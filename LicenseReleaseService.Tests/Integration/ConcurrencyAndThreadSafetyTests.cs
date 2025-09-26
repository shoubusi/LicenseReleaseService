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

namespace LicenseReleaseService.Tests.Integration
{
    /// <summary>
    /// Tests for concurrent access and thread safety of the LicenseQueryEngine
    /// </summary>
    [TestClass]
    public class ConcurrencyAndThreadSafetyTests
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
            _queryEngine.Options.MaxRetries = 2;
            _queryEngine.Options.RetryDelay = TimeSpan.FromMilliseconds(10);

            // Load test data
            _lmstatOutput = await File.ReadAllTextAsync(Path.Combine("TestData", "lmstat_standard_output.txt"));

            // Set up mock results
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
        public async Task ConcurrentAccessToSameServer_ShouldBeThreadSafe()
        {
            // Arrange
            const int concurrentThreads = 100;
            const int operationsPerThread = 10;
            var server = "license-server";
            var port = 27000;
            var results = new ConcurrentBag<LicenseServerStatus>();
            var exceptions = new ConcurrentBag<Exception>();

            // Act
            var stopwatch = Stopwatch.StartNew();

            var tasks = Enumerable.Range(0, concurrentThreads)
                .Select(async threadIndex =>
                {
                    try
                    {
                        for (int i = 0; i < operationsPerThread; i++)
                        {
                            var result = await _queryEngine.QueryLicenseStatusAsync(server, port);
                            results.Add(result);

                            // Small delay to increase chance of race conditions
                            await Task.Delay(TimeSpan.FromMilliseconds(new Random().Next(1, 10)));
                        }
                    }
                    catch (Exception ex)
                    {
                        exceptions.Add(ex);
                        Console.WriteLine($"Thread {threadIndex} failed: {ex}");
                    }
                });

            await Task.WhenAll(tasks);
            stopwatch.Stop();

            var metrics = _queryEngine.GetPerformanceMetrics();

            // Assert
            Assert.AreEqual(0, exceptions.Count, $"Encountered {exceptions.Count} exceptions during concurrent access");
            Assert.AreEqual(concurrentThreads * operationsPerThread, results.Count, "Should have received all results");

            // All results should be valid
            foreach (var result in results)
            {
                Assert.IsNotNull(result);
                Assert.AreEqual(server, result.Server);
                Assert.AreEqual(port, result.Port);
                Assert.IsTrue(result.IsServerUp);
            }

            // Should have reasonable performance even under load
            var throughput = results.Count / (stopwatch.ElapsedMilliseconds / 1000.0);
            Assert.IsTrue(throughput > 50, $"Throughput should be > 50 queries/sec, got {throughput:F2}");

            Console.WriteLine($"Concurrent Access Results:");
            Console.WriteLine($"  Threads: {concurrentThreads}, Operations per thread: {operationsPerThread}");
            Console.WriteLine($"  Total operations: {results.Count}");
            Console.WriteLine($"  Total time: {stopwatch.ElapsedMilliseconds}ms");
            Console.WriteLine($"  Throughput: {throughput:F2} queries/sec");
            Console.WriteLine($"  Exceptions: {exceptions.Count}");
            Console.WriteLine($"  Engine metrics: {metrics}");
        }

        [TestMethod]
        public async Task ConcurrentCacheAccess_ShouldMaintainCacheConsistency()
        {
            // Arrange
            const int concurrentThreads = 50;
            var server = "license-server";
            var port = 27000;
            var cacheHits = 0;
            var cacheMisses = 0;
            var results = new ConcurrentBag<LicenseServerStatus>();

            // Act - First, prime the cache
            await _queryEngine.QueryLicenseStatusAsync(server, port);

            var tasks = Enumerable.Range(0, concurrentThreads)
                .Select(async threadIndex =>
                {
                    for (int i = 0; i < 20; i++)
                    {
                        var result = await _queryEngine.QueryLicenseStatusAsync(server, port);
                        results.Add(result);

                        // Track cache hits vs misses
                        var metrics = _queryEngine.GetPerformanceMetrics();
                        if (metrics.CachedQueries > cacheHits + cacheMisses)
                        {
                            Interlocked.Increment(ref cacheHits);
                        }
                        else
                        {
                            Interlocked.Increment(ref cacheMisses);
                        }

                        // Occasionally invalidate cache to test concurrent invalidation
                        if (new Random().Next(100) < 5) // 5% chance
                        {
                            await _queryEngine.InvalidateServerCacheAsync(server, port);
                        }
                    }
                });

            await Task.WhenAll(tasks);

            var finalMetrics = _queryEngine.GetPerformanceMetrics();

            // Assert
            Assert.AreEqual(concurrentThreads * 20, results.Count, "Should have received all results");

            // All results should be consistent (even if some came from cache, some from live queries)
            var firstResult = results.First();
            foreach (var result in results)
            {
                Assert.AreEqual(firstResult.Server, result.Server);
                Assert.AreEqual(firstResult.Port, result.Port);
                Assert.AreEqual(firstResult.IsServerUp, result.IsServerUp);
            }

            // Should have reasonable cache hit ratio
            var totalCacheOperations = cacheHits + cacheMisses;
            var cacheHitRatio = totalCacheOperations > 0 ? cacheHits / (double)totalCacheOperations : 0;
            Assert.IsTrue(cacheHitRatio > 0.3, $"Cache hit ratio should be > 30%, got {cacheHitRatio:P2}");

            Console.WriteLine($"Concurrent Cache Access Results:");
            Console.WriteLine($"  Total operations: {results.Count}");
            Console.WriteLine($"  Cache hits: {cacheHits}, Cache misses: {cacheMisses}");
            Console.WriteLine($"  Cache hit ratio: {cacheHitRatio:P2}");
            Console.WriteLine($"  Engine cache hit ratio: {finalMetrics.CacheHitRatio:P2}");
            Console.WriteLine($"  Final metrics: {finalMetrics}");
        }

        [TestMethod]
        public async Task ConcurrentMultipleServerQueries_ShouldHandleParallelExecution()
        {
            // Arrange
            const int serverCount = 20;
            const int concurrentUsersPerServer = 10;
            var servers = Enumerable.Range(1, serverCount).Select(i => $"server{i}").ToArray();
            var ports = Enumerable.Range(1, serverCount).Select(i => 27000 + i).ToArray();
            var allResults = new ConcurrentDictionary<string, List<LicenseServerStatus>>();
            var exceptions = new ConcurrentBag<Exception>();

            // Act
            var stopwatch = Stopwatch.StartNew();

            var tasks = servers.Select((server, index) =>
            {
                var port = ports[index];
                return Task.Run(async () =>
                {
                    var serverResults = new List<LicenseServerStatus>();

                    for (int i = 0; i < concurrentUsersPerServer; i++)
                    {
                        try
                        {
                            var result = await _queryEngine.QueryLicenseStatusAsync(server, port);
                            serverResults.Add(result);
                        }
                        catch (Exception ex)
                        {
                            exceptions.Add(ex);
                        }
                    }

                    allResults[$"{server}:{port}"] = serverResults;
                });
            });

            await Task.WhenAll(tasks);
            stopwatch.Stop();

            // Assert
            Assert.AreEqual(0, exceptions.Count, $"Encountered {exceptions.Count} exceptions during multi-server concurrent access");
            Assert.AreEqual(serverCount, allResults.Count, "Should have results for all servers");

            foreach (var serverResult in allResults)
            {
                Assert.AreEqual(concurrentUsersPerServer, serverResult.Value.Count,
                    $"Should have {concurrentUsersPerServer} results for {serverResult.Key}");

                // All results for the same server should be consistent
                var firstResult = serverResult.Value.First();
                foreach (var result in serverResult.Value)
                {
                    Assert.AreEqual(firstResult.Server, result.Server);
                    Assert.AreEqual(firstResult.Port, result.Port);
                    Assert.AreEqual(firstResult.IsServerUp, result.IsServerUp);
                }
            }

            var totalQueries = allResults.Values.Sum(list => list.Count);
            var throughput = totalQueries / (stopwatch.ElapsedMilliseconds / 1000.0);

            Console.WriteLine($"Concurrent Multiple Server Queries Results:");
            Console.WriteLine($"  Servers: {serverCount}, Users per server: {concurrentUsersPerServer}");
            Console.WriteLine($"  Total queries: {totalQueries}");
            Console.WriteLine($"  Total time: {stopwatch.ElapsedMilliseconds}ms");
            Console.WriteLine($"  Throughput: {throughput:F2} queries/sec");
            Console.WriteLine($"  Exceptions: {exceptions.Count}");
        }

        [TestMethod]
        public async Task RaceCondition_CacheUpdate_ShouldHandleConcurrentUpdates()
        {
            // Arrange
            var server = "license-server";
            var port = 27000;
            const int concurrentUpdaters = 20;
            var updateResults = new ConcurrentBag<bool>();
            var cacheStates = new ConcurrentBag<string>();

            // Act
            var tasks = Enumerable.Range(0, concurrentUpdaters)
                .Select(async updaterIndex =>
                {
                    for (int i = 0; i < 10; i++)
                    {
                        try
                        {
                            // Force cache update by querying with a slight delay
                            await Task.Delay(TimeSpan.FromMilliseconds(new Random().Next(1, 50)));

                            var result = await _queryEngine.QueryLicenseStatusAsync(server, port);
                            updateResults.Add(true);

                            // Record cache state
                            var metrics = _queryEngine.GetPerformanceMetrics();
                            cacheStates.Add($"Updater{updaterIndex}-Iteration{i}: Metrics={metrics}");

                            // Occasionally clear cache to force reload
                            if (new Random().Next(100) < 10) // 10% chance
                            {
                                await _queryEngine.ClearAllCacheAsync();
                            }
                        }
                        catch (Exception ex)
                        {
                            updateResults.Add(false);
                            Console.WriteLine($"Updater {updaterIndex} iteration {i} failed: {ex}");
                        }
                    }
                });

            await Task.WhenAll(tasks);

            // Assert
            Assert.AreEqual(concurrentUpdaters * 10, updateResults.Count, "Should have results from all updates");
            Assert.IsTrue(updateResults.All(success => success), "All cache updates should succeed");

            // Final result should be valid
            var finalResult = await _queryEngine.QueryLicenseStatusAsync(server, port);
            Assert.IsNotNull(finalResult);
            Assert.AreEqual(server, finalResult.Server);
            Assert.AreEqual(port, finalResult.Port);
            Assert.IsTrue(finalResult.IsServerUp);

            Console.WriteLine($"Race Condition Cache Update Results:");
            Console.WriteLine($"  Concurrent updaters: {concurrentUpdaters}");
            Console.WriteLine($"  Updates per updater: 10");
            Console.WriteLine($"  Total updates: {updateResults.Count}");
            Console.WriteLine($"  Successful updates: {updateResults.Count(s => s)}");
            Console.WriteLine($"  Failed updates: {updateResults.Count(s => !s)}");
            Console.WriteLine($"  Final cache states recorded: {cacheStates.Count}");
        }

        [TestMethod]
        public async Task ThreadSafety_MetricsCollection_ShouldBeThreadSafe()
        {
            // Arrange
            var server = "license-server";
            var port = 27000;
            const int concurrentThreads = 50;
            const int operationsPerThread = 100;
            var metricsSnapshots = new ConcurrentBag<LicenseQueryMetrics>();

            // Act
            var tasks = Enumerable.Range(0, concurrentThreads)
                .Select(async threadIndex =>
                {
                    for (int i = 0; i < operationsPerThread; i++)
                    {
                        await _queryEngine.QueryLicenseStatusAsync(server, port);

                        // Occasionally capture metrics snapshot
                        if (new Random().Next(100) < 10) // 10% chance
                        {
                            var metrics = _queryEngine.GetPerformanceMetrics();
                            metricsSnapshots.Add(metrics);
                        }
                    }
                });

            await Task.WhenAll(tasks);

            var finalMetrics = _queryEngine.GetPerformanceMetrics();

            // Assert
            Assert.IsTrue(metricsSnapshots.Count > 0, "Should have captured some metrics snapshots");

            // Verify metrics consistency
            long totalQueries = 0;
            long successfulQueries = 0;
            long failedQueries = 0;

            foreach (var snapshot in metricsSnapshots)
            {
                totalQueries = Math.Max(totalQueries, snapshot.TotalQueries);
                successfulQueries = Math.Max(successfulQueries, snapshot.SuccessfulQueries);
                failedQueries = Math.Max(failedQueries, snapshot.FailedQueries);

                // Metrics should be reasonable
                Assert.IsTrue(snapshot.TotalQueries >= 0, "Total queries should be non-negative");
                Assert.IsTrue(snapshot.SuccessfulQueries >= 0, "Successful queries should be non-negative");
                Assert.IsTrue(snapshot.FailedQueries >= 0, "Failed queries should be non-negative");
                Assert.IsTrue(snapshot.AverageQueryTimeMs >= 0, "Average query time should be non-negative");
                Assert.IsTrue(snapshot.CacheHitRatio >= 0 && snapshot.CacheHitRatio <= 1, "Cache hit ratio should be between 0 and 1");
            }

            // Final metrics should reflect all operations
            Assert.AreEqual(concurrentThreads * operationsPerThread, finalMetrics.TotalQueries);
            Assert.AreEqual(concurrentThreads * operationsPerThread, finalMetrics.SuccessfulQueries);
            Assert.AreEqual(0, finalMetrics.FailedQueries);

            Console.WriteLine($"Thread Safety Metrics Collection Results:");
            Console.WriteLine($"  Concurrent threads: {concurrentThreads}, Operations per thread: {operationsPerThread}");
            Console.WriteLine($"  Metrics snapshots captured: {metricsSnapshots.Count}");
            Console.WriteLine($"  Final metrics: {finalMetrics}");
            Console.WriteLine($"  Max observed - Total: {totalQueries}, Success: {successfulQueries}, Failed: {failedQueries}");
        }

        [TestMethod]
        public async Task ConcurrentErrorRecovery_ShouldHandleMultipleFailuresAndRecoveries()
        {
            // Arrange
            var server = "license-server";
            var port = 27000;
            const int concurrentThreads = 30;
            var failureRate = 0.3; // 30% failure rate
            var successes = 0;
            var failures = 0;
            var recoveries = 0;

            // Set up mock that fails sometimes
            var callCount = 0;
            _testProcessExecutor.SetMockResult("lmutil.exe", $"-c {server}@{port}", () =>
            {
                callCount++;
                if (new Random().NextDouble() < failureRate)
                {
                    return new ProcessExecutionResult
                    {
                        ExitCode = 1,
                        Error = "Random failure",
                        ExecutionTime = TimeSpan.FromMilliseconds(100),
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

            // Enable retries
            _queryEngine.Options.MaxRetries = 3;
            _queryEngine.Options.RetryDelay = TimeSpan.FromMilliseconds(5);

            // Act
            var tasks = Enumerable.Range(0, concurrentThreads)
                .Select(async threadIndex =>
                {
                    for (int i = 0; i < 20; i++)
                    {
                        try
                        {
                            var result = await _queryEngine.QueryLicenseStatusAsync(server, port);
                            Interlocked.Increment(ref successes);
                        }
                        catch
                        {
                            Interlocked.Increment(ref failures);
                        }
                    }
                });

            await Task.WhenAll(tasks);

            var metrics = _queryEngine.GetPerformanceMetrics();

            // Assert
            var totalAttempts = successes + failures;
            var successRate = successes / (double)totalAttempts;

            // Should have reasonable success rate despite failures (due to retries)
            Assert.IsTrue(successRate > 0.8, $"Success rate should be > 80% despite {failureRate:P0} failure rate, got {successRate:P2}");

            Console.WriteLine($"Concurrent Error Recovery Results:");
            Console.WriteLine($"  Concurrent threads: {concurrentThreads}, Operations per thread: 20");
            Console.WriteLine($"  Total attempts: {totalAttempts}");
            Console.WriteLine($"  Successes: {successes}, Failures: {failures}");
            Console.WriteLine($"  Success rate: {successRate:P2}");
            Console.WriteLine($"  Engine metrics: {metrics}");
            Console.WriteLine($"  Process executor calls: {callCount}");
        }

        [TestMethod]
        public async Task CancellationTokenHandling_ShouldGracefullyCancelConcurrentOperations()
        {
            // Arrange
            var server = "license-server";
            var port = 27000;
            const int concurrentThreads = 20;
            var cts = new CancellationTokenSource();
            var cancelledTasks = 0;
            var successfulTasks = 0;

            // Set up mock that sometimes takes a long time
            var callCount = 0;
            _testProcessExecutor.SetMockResult("lmutil.exe", $"-c {server}@{port}", () =>
            {
                callCount++;
                if (callCount % 5 == 0) // Every 5th call is slow
                {
                    return new ProcessExecutionResult
                    {
                        ExitCode = 0,
                        Output = _lmstatOutput,
                        ExecutionTime = TimeSpan.FromMilliseconds(2000),
                        Success = true
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

            // Act - Start operations and cancel after a delay
            var tasks = Enumerable.Range(0, concurrentThreads)
                .Select(async threadIndex =>
                {
                    try
                    {
                        // Each thread performs multiple operations
                        for (int i = 0; i < 10; i++)
                        {
                            await _queryEngine.QueryLicenseStatusAsync(server, port, cts.Token);
                            Interlocked.Increment(ref successfulTasks);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        Interlocked.Increment(ref cancelledTasks);
                    }
                });

            // Cancel after a short delay
            await Task.Delay(500);
            cts.Cancel();

            try
            {
                await Task.WhenAll(tasks);
            }
            catch (OperationCanceledException)
            {
                // Expected
            }

            // Assert
            Assert.IsTrue(cancelledTasks > 0, $"Should have some cancelled tasks, got {cancelledTasks}");
            Assert.IsTrue(successfulTasks > 0, $"Should have some successful tasks, got {successfulTasks}");

            Console.WriteLine($"Cancellation Handling Results:");
            Console.WriteLine($"  Concurrent threads: {concurrentThreads}");
            Console.WriteLine($"  Successful operations: {successfulTasks}");
            Console.WriteLine($"  Cancelled operations: {cancelledTasks}");
            Console.WriteLine($"  Total operations attempted: {successfulTasks + cancelledTasks}");
        }

        [TestMethod]
        public async Task MemoryLeakTest_ConcurrentOperations_ShouldNotLeakMemory()
        {
            // Arrange
            var server = "license-server";
            var port = 27000;
            const int iterations = 1000;
            const int concurrentThreads = 10;
            var memoryMeasurements = new List<long>();

            // Force GC and get baseline
            GC.Collect();
            GC.WaitForPendingFinalizers();
            var initialMemory = GC.GetTotalMemory(true);

            // Act
            for (int iteration = 0; iteration < iterations; iteration++)
            {
                var tasks = Enumerable.Range(0, concurrentThreads)
                    .Select(async _ =>
                    {
                        await _queryEngine.QueryLicenseStatusAsync(server, port);
                        await _queryEngine.QueryFeaturesAsync(server, port);
                        await _queryEngine.QueryActiveUsersAsync(server, port);
                    });

                await Task.WhenAll(tasks);

                // Periodically measure memory
                if (iteration % 100 == 0)
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
            // Memory increase should be reasonable for the workload
            var maxMemory = memoryMeasurements.Any() ? memoryMeasurements.Max() : finalMemory;
            var memoryIncreasePerIteration = memoryIncrease / (double)iterations;

            Assert.IsTrue(memoryIncrease < 100 * 1024 * 1024, $"Total memory increase {memoryIncrease / 1024 / 1024:F2}MB should be less than 100MB");
            Assert.IsTrue(memoryIncreasePerIteration < 1024, $"Memory increase per iteration {memoryIncreasePerIteration:F2} bytes should be less than 1KB");

            Console.WriteLine($"Memory Leak Test Results:");
            Console.WriteLine($"  Iterations: {iterations}, Concurrent threads: {concurrentThreads}");
            Console.WriteLine($"  Initial memory: {initialMemory / 1024 / 1024:F2}MB");
            Console.WriteLine($"  Final memory: {finalMemory / 1024 / 1024:F2}MB");
            Console.WriteLine($"  Memory increase: {memoryIncrease / 1024 / 1024:F2}MB");
            Console.WriteLine($"  Memory increase per iteration: {memoryIncreasePerIteration:F2} bytes");
            Console.WriteLine($"  Memory measurements taken: {memoryMeasurements.Count}");
        }

        private void SetupMockResults()
        {
            // Set up basic server result
            _testProcessExecutor.SetMockResult("lmutil.exe", "-c license-server@27000", new ProcessExecutionResult
            {
                ExitCode = 0,
                Output = _lmstatOutput,
                ExecutionTime = TimeSpan.FromMilliseconds(50),
                Success = true
            });

            // Set up results for multiple servers
            for (int i = 1; i <= 20; i++)
            {
                var server = $"server{i}";
                var port = 27000 + i;
                _testProcessExecutor.SetMockResult("lmutil.exe", $"-c {server}@{port}", new ProcessExecutionResult
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