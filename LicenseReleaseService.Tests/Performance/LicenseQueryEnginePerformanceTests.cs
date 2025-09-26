using System;
using System.Collections.Concurrent;
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

namespace LicenseReleaseService.Tests.Performance
{
    /// <summary>
    /// Performance tests for the LicenseQueryEngine class that test load, stress, and performance characteristics
    /// </summary>
    [TestClass]
    public class LicenseQueryEnginePerformanceTests
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
            _queryEngine.Options.CacheExpiration = TimeSpan.FromMinutes(5);
            _queryEngine.Options.QueryTimeout = TimeSpan.FromSeconds(30);

            // Load test data
            _lmstatOutput = await File.ReadAllTextAsync(Path.Combine("TestData", "lmstat_standard_output.txt"));

            // Set up mock results
            _testProcessExecutor.SetMockResult("lmutil.exe", "-c license-server@27000", new ProcessExecutionResult
            {
                ExitCode = 0,
                Output = _lmstatOutput,
                ExecutionTime = TimeSpan.FromMilliseconds(50),
                Success = true
            });
        }

        [TestCleanup]
        public void TestCleanup()
        {
            _queryEngine?.ClearAllCacheAsync().GetAwaiter().GetResult();
            _testCacheManager?.Clear();
            _testProcessExecutor?.Reset();
        }

        [TestMethod]
        public async Task SingleQueryPerformance_ShouldMeetPerformanceRequirements()
        {
            // Arrange
            const int warmupRuns = 5;
            const int testRuns = 50;
            var server = "license-server";
            var port = 27000;
            var responseTimes = new List<long>();

            // Warmup
            for (int i = 0; i < warmupRuns; i++)
            {
                await _queryEngine.QueryLicenseStatusAsync(server, port);
            }

            _queryEngine.ResetPerformanceMetrics();

            // Act
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
            Assert.AreEqual(testRuns, metrics.TotalQueries);
            Assert.AreEqual(testRuns, metrics.SuccessfulQueries);
            Assert.AreEqual(0, metrics.FailedQueries);

            var averageResponseTime = responseTimes.Average();
            var maxResponseTime = responseTimes.Max();
            var minResponseTime = responseTimes.Min();

            // Performance assertions - should be much faster than 2 seconds
            Assert.IsTrue(averageResponseTime < 100, $"Average response time {averageResponseTime}ms should be less than 100ms");
            Assert.IsTrue(maxResponseTime < 500, $"Max response time {maxResponseTime}ms should be less than 500ms");
            Assert.IsTrue(minResponseTime > 0, "Min response time should be greater than 0");

            Console.WriteLine($"Single Query Performance Results:");
            Console.WriteLine($"  Total test time: {stopwatch.ElapsedMilliseconds}ms");
            Console.WriteLine($"  Average response time: {averageResponseTime:F2}ms");
            Console.WriteLine($"  Min response time: {minResponseTime:F2}ms");
            Console.WriteLine($"  Max response time: {maxResponseTime:F2}ms");
            Console.WriteLine($"  Queries per second: {testRuns / (stopwatch.ElapsedMilliseconds / 1000.0):F2}");
            Console.WriteLine($"  Engine metrics: {metrics}");
        }

        [TestMethod]
        public async Task ConcurrentQueryPerformance_ShouldHandle100ConcurrentRequests()
        {
            // Arrange
            const int concurrentUsers = 100;
            const int requestsPerUser = 5;
            var server = "license-server";
            var port = 27000;
            var allResponseTimes = new List<long>();
            var exceptions = new List<Exception>();

            // Act
            var stopwatch = Stopwatch.StartNew();

            var tasks = Enumerable.Range(0, concurrentUsers)
                .Select(async userIndex =>
                {
                    try
                    {
                        var userResponseTimes = new List<long>();
                        for (int i = 0; i < requestsPerUser; i++)
                        {
                            var queryStopwatch = Stopwatch.StartNew();
                            await _queryEngine.QueryLicenseStatusAsync(server, port);
                            queryStopwatch.Stop();
                            userResponseTimes.Add(queryStopwatch.ElapsedMilliseconds);
                        }
                        lock (allResponseTimes)
                        {
                            allResponseTimes.AddRange(userResponseTimes);
                        }
                    }
                    catch (Exception ex)
                    {
                        lock (exceptions)
                        {
                            exceptions.Add(ex);
                        }
                    }
                });

            await Task.WhenAll(tasks);
            stopwatch.Stop();

            var metrics = _queryEngine.GetPerformanceMetrics();

            // Assert
            Assert.AreEqual(0, exceptions.Count, $"Encountered {exceptions.Count} exceptions during concurrent testing: {string.Join(", ", exceptions.Select(e => e.Message))}");
            Assert.AreEqual(concurrentUsers * requestsPerUser, metrics.TotalQueries);
            Assert.AreEqual(concurrentUsers * requestsPerUser, metrics.SuccessfulQueries);
            Assert.AreEqual(0, metrics.FailedQueries);

            var averageResponseTime = allResponseTimes.Average();
            var maxResponseTime = allResponseTimes.Max();
            var minResponseTime = allResponseTimes.Min();
            var throughput = (concurrentUsers * requestsPerUser) / (stopwatch.ElapsedMilliseconds / 1000.0);

            // Performance assertions for concurrent load
            Assert.IsTrue(averageResponseTime < 200, $"Average response time {averageResponseTime}ms should be less than 200ms under load");
            Assert.IsTrue(maxResponseTime < 1000, $"Max response time {maxResponseTime}ms should be less than 1000ms under load");
            Assert.IsTrue(throughput > 50, $"Throughput {throughput:F2} queries/sec should be greater than 50");

            Console.WriteLine($"Concurrent Query Performance Results:");
            Console.WriteLine($"  Concurrent users: {concurrentUsers}");
            Console.WriteLine($"  Requests per user: {requestsPerUser}");
            Console.WriteLine($"  Total requests: {concurrentUsers * requestsPerUser}");
            Console.WriteLine($"  Total test time: {stopwatch.ElapsedMilliseconds}ms");
            Console.WriteLine($"  Average response time: {averageResponseTime:F2}ms");
            Console.WriteLine($"  Min response time: {minResponseTime:F2}ms");
            Console.WriteLine($"  Max response time: {maxResponseTime:F2}ms");
            Console.WriteLine($"  Throughput: {throughput:F2} queries/sec");
            Console.WriteLine($"  Engine metrics: {metrics}");
        }

        [TestMethod]
        public async Task MemoryUsagePerformance_ShouldStayWithinMemoryLimits()
        {
            // Arrange
            const int iterationCount = 1000;
            var server = "license-server";
            var port = 27000;
            var initialMemory = GC.GetTotalMemory(true);
            var memoryMeasurements = new List<long>();

            // Act
            for (int i = 0; i < iterationCount; i++)
            {
                // Perform various operations
                await _queryEngine.QueryLicenseStatusAsync(server, port);
                await _queryEngine.QueryFeaturesAsync(server, port);
                await _queryEngine.QueryActiveUsersAsync(server, port);

                if (i % 100 == 0)
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    var currentMemory = GC.GetTotalMemory(false);
                    memoryMeasurements.Add(currentMemory);
                }
            }

            // Final memory measurement
            GC.Collect();
            GC.WaitForPendingFinalizers();
            var finalMemory = GC.GetTotalMemory(true);
            var memoryIncrease = finalMemory - initialMemory;

            // Assert
            // Memory should not grow uncontrollably - less than 50MB increase for 1000 iterations
            Assert.IsTrue(memoryIncrease < 50 * 1024 * 1024, $"Memory increase {memoryIncrease / 1024 / 1024:F2}MB should be less than 50MB");

            Console.WriteLine($"Memory Usage Performance Results:");
            Console.WriteLine($"  Initial memory: {initialMemory / 1024 / 1024:F2}MB");
            Console.WriteLine($"  Final memory: {finalMemory / 1024 / 1024:F2}MB");
            Console.WriteLine($"  Memory increase: {memoryIncrease / 1024 / 1024:F2}MB");
            Console.WriteLine($"  Memory measurements: {memoryMeasurements.Count} points taken");
            if (memoryMeasurements.Count > 0)
            {
                Console.WriteLine($"  Average memory during test: {memoryMeasurements.Average() / 1024 / 1024:F2}MB");
                Console.WriteLine($"  Max memory during test: {memoryMeasurements.Max() / 1024 / 1024:F2}MB");
            }
        }

        [TestMethod]
        public async Task CachingPerformance_ShouldImproveResponseTimeWithCache()
        {
            // Arrange
            const int testRuns = 100;
            var server = "license-server";
            var port = 27000;
            var cacheEnabledTimes = new List<long>();
            var cacheDisabledTimes = new List<long>();

            // Test with caching disabled
            _queryEngine.Options.EnableCaching = false;
            _queryEngine.ResetPerformanceMetrics();

            for (int i = 0; i < testRuns; i++)
            {
                var stopwatch = Stopwatch.StartNew();
                await _queryEngine.QueryLicenseStatusAsync(server, port);
                stopwatch.Stop();
                cacheDisabledTimes.Add(stopwatch.ElapsedMilliseconds);
            }

            var noCacheMetrics = _queryEngine.GetPerformanceMetrics();

            // Test with caching enabled
            _queryEngine.Options.EnableCaching = true;
            _queryEngine.ResetPerformanceMetrics();
            _testCacheManager.Clear();

            // Prime the cache
            await _queryEngine.QueryLicenseStatusAsync(server, port);

            for (int i = 0; i < testRuns; i++)
            {
                var stopwatch = Stopwatch.StartNew();
                await _queryEngine.QueryLicenseStatusAsync(server, port);
                stopwatch.Stop();
                cacheEnabledTimes.Add(stopwatch.ElapsedMilliseconds);
            }

            var cacheMetrics = _queryEngine.GetPerformanceMetrics();

            // Assert
            var avgWithoutCache = cacheDisabledTimes.Average();
            var avgWithCache = cacheEnabledTimes.Average();
            var improvementRatio = avgWithoutCache / avgWithCache;

            Assert.IsTrue(improvementRatio > 2, $"Cache should provide at least 2x improvement, got {improvementRatio:F2}x");
            Assert.IsTrue(cacheMetrics.CacheHitRatio > 0.8, $"Cache hit ratio should be > 80%, got {cacheMetrics.CacheHitRatio:P2}");

            Console.WriteLine($"Caching Performance Results:");
            Console.WriteLine($"  Without cache - Avg: {avgWithoutCache:F2}ms, Min: {cacheDisabledTimes.Min():F2}ms, Max: {cacheDisabledTimes.Max():F2}ms");
            Console.WriteLine($"  With cache - Avg: {avgWithCache:F2}ms, Min: {cacheEnabledTimes.Min():F2}ms, Max: {cacheEnabledTimes.Max():F2}ms");
            Console.WriteLine($"  Improvement: {improvementRatio:F2}x faster");
            Console.WriteLine($"  Cache hit ratio: {cacheMetrics.CacheHitRatio:P2}");
            Console.WriteLine($"  Cache metrics: {cacheMetrics.CachedQueries} cached out of {cacheMetrics.TotalQueries} total");
        }

        [TestMethod]
        public async Task StressTest_ShouldHandleHighLoadForExtendedPeriod()
        {
            // Arrange
            const int durationSeconds = 30;
            const int concurrentUsers = 50;
            var server = "license-server";
            var port = 27000;
            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(durationSeconds));
            var successfulQueries = 0;
            var failedQueries = 0;
            var responseTimes = new ConcurrentQueue<long>();
            var exceptions = new ConcurrentQueue<Exception>();

            // Act
            var stopwatch = Stopwatch.StartNew();

            var tasks = Enumerable.Range(0, concurrentUsers)
                .Select(async _ =>
                {
                    try
                    {
                        while (!cts.Token.IsCancellationRequested)
                        {
                            var queryStopwatch = Stopwatch.StartNew();
                            await _queryEngine.QueryLicenseStatusAsync(server, port, cts.Token);
                            queryStopwatch.Stop();

                            responseTimes.Enqueue(queryStopwatch.ElapsedMilliseconds);
                            Interlocked.Increment(ref successfulQueries);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        // Expected during cancellation
                    }
                    catch (Exception ex)
                    {
                        exceptions.Enqueue(ex);
                        Interlocked.Increment(ref failedQueries);
                    }
                });

            await Task.WhenAll(tasks);
            stopwatch.Stop();

            var metrics = _queryEngine.GetPerformanceMetrics();

            // Assert
            Assert.IsTrue(failedQueries == 0, $"Encountered {failedQueries} failed queries during stress test");
            Assert.IsTrue(exceptions.Count == 0, $"Encountered {exceptions.Count} exceptions during stress test");

            var throughput = successfulQueries / (stopwatch.ElapsedMilliseconds / 1000.0);
            var avgResponseTime = responseTimes.Any() ? responseTimes.Average() : 0;

            // Stress test assertions
            Assert.IsTrue(successfulQueries > 1000, $"Should handle at least 1000 queries in {durationSeconds} seconds, got {successfulQueries}");
            Assert.IsTrue(throughput > 30, $"Throughput should be at least 30 queries/sec, got {throughput:F2}");
            Assert.IsTrue(avgResponseTime < 1000, $"Average response time should be less than 1000ms under stress, got {avgResponseTime:F2}ms");

            Console.WriteLine($"Stress Test Results:");
            Console.WriteLine($"  Duration: {stopwatch.ElapsedMilliseconds}ms ({durationSeconds} seconds)");
            Console.WriteLine($"  Concurrent users: {concurrentUsers}");
            Console.WriteLine($"  Successful queries: {successfulQueries}");
            Console.WriteLine($"  Failed queries: {failedQueries}");
            Console.WriteLine($"  Throughput: {throughput:F2} queries/sec");
            Console.WriteLine($"  Average response time: {avgResponseTime:F2}ms");
            Console.WriteLine($"  Engine metrics: {metrics}");

            if (exceptions.Any())
            {
                Console.WriteLine($"  Exceptions encountered: {exceptions.Count}");
                foreach (var ex in exceptions.Take(5)) // Show first 5 exceptions
                {
                    Console.WriteLine($"    - {ex.GetType().Name}: {ex.Message}");
                }
            }
        }

        [TestMethod]
        public async Task MultipleServerPerformance_ShouldScaleWithServerCount()
        {
            // Arrange
            var serverCounts = new[] { 1, 5, 10, 20, 50 };
            var results = new Dictionary<int, PerformanceResult>();

            foreach (var serverCount in serverCounts)
            {
                var servers = Enumerable.Range(1, serverCount).Select(i => $"server{i}").ToArray();
                var ports = Enumerable.Range(1, serverCount).Select(i => 27000 + i).ToArray();

                // Set up mock results for each server
                foreach (var (server, port) in servers.Zip(ports, (s, p) => (s, p)))
                {
                    _testProcessExecutor.SetMockResult("lmutil.exe", $"-c {server}@{port}", new ProcessExecutionResult
                    {
                        ExitCode = 0,
                        Output = _lmstatOutput,
                        ExecutionTime = TimeSpan.FromMilliseconds(50),
                        Success = true
                    });
                }

                _queryEngine.ResetPerformanceMetrics();

                // Act
                var stopwatch = Stopwatch.StartNew();
                var result = await _queryEngine.QueryMultipleServersAsync(servers, ports);
                stopwatch.Stop();

                var metrics = _queryEngine.GetPerformanceMetrics();

                results[serverCount] = new PerformanceResult
                {
                    ServerCount = serverCount,
                    TotalTimeMs = stopwatch.ElapsedMilliseconds,
                    AverageTimePerServer = stopwatch.ElapsedMilliseconds / (double)serverCount,
                    QueriesPerSecond = serverCount / (stopwatch.ElapsedMilliseconds / 1000.0),
                    TotalQueries = metrics.TotalQueries,
                    SuccessRate = metrics.SuccessRate
                };

                Console.WriteLine($"Server count {serverCount}: {stopwatch.ElapsedMilliseconds}ms total, {stopwatch.ElapsedMilliseconds / (double)serverCount:F2}ms per server");
            }

            // Assert
            Assert.IsTrue(results.Count > 0, "Should have performance results for multiple server counts");

            // Verify that performance scales reasonably (not linearly worse with each additional server)
            var singleServerTime = results[1].TotalTimeMs;
            var tenServerTime = results[10].TotalTimeMs;

            // Should be better than 10x slower for 10x servers (due to concurrent execution)
            Assert.IsTrue(tenServerTime < singleServerTime * 8,
                $"10 servers should be faster than 8x single server time. Single: {singleServerTime}ms, 10 servers: {tenServerTime}ms");

            Console.WriteLine($"Multiple Server Performance Scaling:");
            foreach (var result in results.Values.OrderBy(r => r.ServerCount))
            {
                Console.WriteLine($"  {result.ServerCount,2} servers: {result.TotalTimeMs,4}ms total, " +
                    $"{result.AverageTimePerServer,5:F1}ms/server, {result.QueriesPerSecond,5:F1} queries/sec, " +
                    $"{result.SuccessRate:P2} success rate");
            }
        }

        [TestMethod]
        public async Task LargeDatasetPerformance_ShouldHandleLargeLicenseDatasets()
        {
            // Arrange - Create a large license dataset
            var server = "license-server";
            var port = 27000;
            var largeOutput = GenerateLargeLmstatOutput(1000, 100); // 1000 features, 100 users each

            _testProcessExecutor.SetMockResult("lmutil.exe", $"-c {server}@{port}", new ProcessExecutionResult
            {
                ExitCode = 0,
                Output = largeOutput,
                ExecutionTime = TimeSpan.FromMilliseconds(200), // Simulate slower parsing
                Success = true
            });

            const int testRuns = 20;
            var responseTimes = new List<long>();

            // Act
            for (int i = 0; i < testRuns; i++)
            {
                var stopwatch = Stopwatch.StartNew();
                var result = await _queryEngine.QueryLicenseStatusAsync(server, port);
                stopwatch.Stop();
                responseTimes.Add(stopwatch.ElapsedMilliseconds);

                // Verify the result is reasonable
                Assert.IsNotNull(result);
                Assert.IsTrue(result.FeatureDetails.Count > 500, $"Should have many features, got {result.FeatureDetails.Count}");
                Assert.IsTrue(result.TotalUsers > 1000, $"Should have many users, got {result.TotalUsers}");
            }

            var metrics = _queryEngine.GetPerformanceMetrics();

            // Assert
            var averageResponseTime = responseTimes.Average();
            var maxResponseTime = responseTimes.Max();

            // Large dataset should still perform reasonably
            Assert.IsTrue(averageResponseTime < 1000, $"Average response time {averageResponseTime}ms should be less than 1000ms for large dataset");
            Assert.IsTrue(maxResponseTime < 3000, $"Max response time {maxResponseTime}ms should be less than 3000ms for large dataset");

            Console.WriteLine($"Large Dataset Performance Results:");
            Console.WriteLine($"  Dataset size: ~1000 features, ~100,000 users");
            Console.WriteLine($"  Test runs: {testRuns}");
            Console.WriteLine($"  Average response time: {averageResponseTime:F2}ms");
            Console.WriteLine($"  Min response time: {responseTimes.Min():F2}ms");
            Console.WriteLine($"  Max response time: {maxResponseTime:F2}ms");
            Console.WriteLine($"  Engine metrics: {metrics}");
        }

        [TestMethod]
        public async Task TimeoutHandlingPerformance_ShouldRespectTimeoutsUnderLoad()
        {
            // Arrange
            var server = "license-server";
            var port = 27000;
            const int timeoutMs = 1000;

            _queryEngine.Options.QueryTimeout = TimeSpan.FromMilliseconds(timeoutMs);

            // Set up a mock that sometimes times out
            var callCount = 0;
            _testProcessExecutor.SetMockResult("lmutil.exe", $"-c {server}@{port}", () =>
            {
                callCount++;
                if (callCount % 4 == 0) // Every 4th call times out
                {
                    Thread.Sleep(timeoutMs + 500); // Sleep longer than timeout
                    return new ProcessExecutionResult
                    {
                        ExitCode = -1,
                        Error = "Timeout",
                        ExecutionTime = TimeSpan.FromMilliseconds(timeoutMs + 500),
                        Success = false
                    };
                }
                else
                {
                    return new ProcessExecutionResult
                    {
                        ExitCode = 0,
                        Output = _lmstatOutput,
                        ExecutionTime = TimeSpan.FromMilliseconds(50),
                        Success = true
                    };
                }
            });

            const int totalRequests = 100;
            var timeouts = 0;
            var successes = 0;
            var responseTimes = new List<long>();

            // Act
            var stopwatch = Stopwatch.StartNew();

            var tasks = Enumerable.Range(0, totalRequests)
                .Select(async _ =>
                {
                    try
                    {
                        var queryStopwatch = Stopwatch.StartNew();
                        await _queryEngine.QueryLicenseStatusAsync(server, port);
                        queryStopwatch.Stop();

                        responseTimes.Add(queryStopwatch.ElapsedMilliseconds);
                        Interlocked.Increment(ref successes);
                    }
                    catch (Exception ex) when (ex.Message.Contains("timeout") || ex.Message.Contains("Timeout"))
                    {
                        Interlocked.Increment(ref timeouts);
                    }
                    catch (Exception ex)
                    {
                        Assert.Fail($"Unexpected exception: {ex}");
                    }
                });

            await Task.WhenAll(tasks);
            stopwatch.Stop();

            var metrics = _queryEngine.GetPerformanceMetrics();

            // Assert
            var expectedTimeouts = totalRequests / 4; // Approximately 25% should timeout
            Assert.IsTrue(Math.Abs(timeouts - expectedTimeouts) <= 10,
                $"Expected ~{expectedTimeouts} timeouts, got {timeouts}");

            // Successful requests should respect timeout
            var successfulResponseTimes = responseTimes.Where(t => t <= timeoutMs * 2).ToList();
            Assert.IsTrue(successfulResponseTimes.Count == successes,
                $"All successful requests should complete within reasonable time");

            Console.WriteLine($"Timeout Handling Performance Results:");
            Console.WriteLine($"  Total requests: {totalRequests}");
            Console.WriteLine($"  Successful requests: {successes}");
            Console.WriteLine($"  Timeout requests: {timeouts}");
            Console.WriteLine($"  Success rate: {successes / (double)totalRequests:P2}");
            Console.WriteLine($"  Total test time: {stopwatch.ElapsedMilliseconds}ms");
            Console.WriteLine($"  Engine metrics: {metrics}");
        }

        [TestMethod]
        public async Task PerformanceMetricsAccuracy_ShouldTrackMetricsCorrectly()
        {
            // Arrange
            var server = "license-server";
            var port = 27000;
            const int totalQueries = 1000;
            const int expectedFailures = 50; // 5% failure rate

            var callCount = 0;
            _testProcessExecutor.SetMockResult("lmutil.exe", $"-c {server}@{port}", () =>
            {
                callCount++;
                if (callCount % (totalQueries / expectedFailures) == 0)
                {
                    return new ProcessExecutionResult
                    {
                        ExitCode = 1,
                        Error = "Simulated failure",
                        ExecutionTime = TimeSpan.FromMilliseconds(10),
                        Success = false
                    };
                }
                else
                {
                    return new ProcessExecutionResult
                    {
                        ExitCode = 0,
                        Output = _lmstatOutput,
                        ExecutionTime = TimeSpan.FromMilliseconds(50),
                        Success = true
                    };
                }
            });

            // Act
            _queryEngine.ResetPerformanceMetrics();

            var tasks = Enumerable.Range(0, totalQueries)
                .Select(async _ =>
                {
                    try
                    {
                        await _queryEngine.QueryLicenseStatusAsync(server, port);
                    }
                    catch
                    {
                        // Expected failures
                    }
                });

            await Task.WhenAll(tasks);

            var metrics = _queryEngine.GetPerformanceMetrics();

            // Assert
            Assert.AreEqual(totalQueries, metrics.TotalQueries);
            Assert.AreEqual(totalQueries - expectedFailures, metrics.SuccessfulQueries);
            Assert.AreEqual(expectedFailures, metrics.FailedQueries);
            Assert.IsTrue(metrics.AverageQueryTimeMs > 0);
            Assert.IsTrue(metrics.MinQueryTimeMs > 0);
            Assert.IsTrue(metrics.MaxQueryTimeMs >= metrics.MinQueryTimeMs);
            Assert.AreEqual(0, metrics.CachedQueries); // No caching in this test

            var calculatedSuccessRate = metrics.SuccessfulQueries / (double)metrics.TotalQueries;
            var expectedSuccessRate = (totalQueries - expectedFailures) / (double)totalQueries;
            Assert.AreEqual(expectedSuccessRate, calculatedSuccessRate, 0.01, "Success rate should match expected");

            Console.WriteLine($"Performance Metrics Accuracy Results:");
            Console.WriteLine($"  Expected: {totalQueries} total, {totalQueries - expectedFailures} success, {expectedFailures} failed");
            Console.WriteLine($"  Actual: {metrics.TotalQueries} total, {metrics.SuccessfulQueries} success, {metrics.FailedQueries} failed");
            Console.WriteLine($"  Success rate: expected {expectedSuccessRate:P2}, actual {calculatedSuccessRate:P2}");
            Console.WriteLine($"  Average time: {metrics.AverageQueryTimeMs:F2}ms");
            Console.WriteLine($"  Time range: {metrics.MinQueryTimeMs:F2}ms - {metrics.MaxQueryTimeMs:F2}ms");
        }

        private string GenerateLargeLmstatOutput(int featureCount, int usersPerFeature)
        {
            var output = new System.Text.StringBuilder();
            output.AppendLine("License server status: 27000@license-server");
            output.AppendLine("    License file(s) on license-server:");
            output.AppendLine("    SOLIDWORKS: license server UP (MASTER) v11.16.2");
            output.AppendLine();
            output.AppendLine("Vendor daemon status (on license-server):");
            output.AppendLine("    solidworks: UP v11.16.2");
            output.AppendLine();

            var random = new Random(42); // Seed for reproducible results

            for (int f = 1; f <= featureCount; f++)
            {
                var featureName = $"feature{f}";
                var totalLicenses = random.Next(50, 500);
                var usedLicenses = random.Next(0, totalLicenses);

                output.AppendLine($"Users of {featureName}:  (Total of {totalLicenses} licenses issued;  Total of {usedLicenses} licenses in use)");

                for (int u = 1; u <= usedLicenses; u++)
                {
                    var username = $"user{random.Next(1, 10000):D5}";
                    var workstation = $"workstation{random.Next(1, 1000):D3}";
                    var version = $"v{random.Next(2020, 2024)}.{random.Next(1, 10)}";
                    var handle = random.Next(1, 1000);
                    var hoursAgo = random.Next(1, 72);
                    var startTime = DateTime.Now.AddHours(-hoursAgo);

                    output.AppendLine($"\"{username}\" {workstation} ({version}) (license-server/27000 {handle}), start {startTime:ddd M/d HH:mm}");
                }

                output.AppendLine();
            }

            return output.ToString();
        }

        private class PerformanceResult
        {
            public int ServerCount { get; set; }
            public long TotalTimeMs { get; set; }
            public double AverageTimePerServer { get; set; }
            public double QueriesPerSecond { get; set; }
            public long TotalQueries { get; set; }
            public double SuccessRate { get; set; }
        }
    }
}