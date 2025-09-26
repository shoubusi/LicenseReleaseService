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
    /// Integration tests for the LicenseQueryEngine class that test end-to-end functionality
    /// </summary>
    [TestClass]
    public class LicenseQueryEngineIntegrationTests
    {
        private ICacheManager _cacheManager;
        private IProcessExecutor _processExecutor;
        private LmstatOutputParser _outputParser;
        private LicenseQueryEngine _queryEngine;
        private TestProcessExecutor _testProcessExecutor;
        private TestCacheManager _testCacheManager;

        [TestInitialize]
        public void TestInitialize()
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
        }

        [TestCleanup]
        public void TestCleanup()
        {
            _queryEngine?.ClearAllCacheAsync().GetAwaiter().GetResult();
            _testCacheManager?.Clear();
            _testProcessExecutor?.Reset();
        }

        [TestMethod]
        public async Task QueryLicenseStatusAsync_WithRealLmstatOutput_ShouldParseCorrectly()
        {
            // Arrange
            var server = "license-server";
            var port = 27000;
            var lmstatOutput = await File.ReadAllTextAsync(Path.Combine("TestData", "lmstat_standard_output.txt"));

            _testProcessExecutor.SetMockResult("lmutil.exe", $"-c {server}@{port}", new ProcessExecutionResult
            {
                ExitCode = 0,
                Output = lmstatOutput,
                ExecutionTime = TimeSpan.FromMilliseconds(150),
                Success = true
            });

            var stopwatch = Stopwatch.StartNew();

            // Act
            var result = await _queryEngine.QueryLicenseStatusAsync(server, port);
            stopwatch.Stop();

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(server, result.Server);
            Assert.AreEqual(port, result.Port);
            Assert.IsTrue(result.IsServerUp);
            Assert.IsTrue(result.IsHealthy);
            Assert.IsTrue(result.ResponseTimeMs > 0);
            Assert.IsTrue(result.ResponseTimeMs <= 200); // Should be fast

            // Verify feature details were parsed correctly
            Assert.IsNotNull(result.FeatureDetails);
            Assert.IsTrue(result.FeatureDetails.Count > 0);

            // Check for expected features
            Assert.IsTrue(result.FeatureDetails.ContainsKey("solidworks"));
            Assert.IsTrue(result.FeatureDetails.ContainsKey("toolbox"));
            Assert.IsTrue(result.FeatureDetails.ContainsKey("photoview360"));

            // Verify license counts
            var solidworksFeature = result.FeatureDetails["solidworks"];
            Assert.AreEqual(100, solidworksFeature.TotalLicenses);
            Assert.AreEqual(25, solidworksFeature.UsedLicenses);
            Assert.AreEqual(75, solidworksFeature.AvailableLicenses);

            // Verify active users
            Assert.AreEqual(3, solidworksFeature.ActiveUsers.Count);
            Assert.IsTrue(solidworksFeature.ActiveUsers.Any(u => u.UserHost == "johndoe"));
            Assert.IsTrue(solidworksFeature.ActiveUsers.Any(u => u.UserHost == "janedoe"));
            Assert.IsTrue(solidworksFeature.ActiveUsers.Any(u => u.UserHost == "bobsmith"));

            // Test logging
            Console.WriteLine($"License query completed in {stopwatch.ElapsedMilliseconds}ms");
            Console.WriteLine($"Server: {result.Server}:{result.Port}");
            Console.WriteLine($"Total Features: {result.FeatureDetails.Count}");
            Console.WriteLine($"Total Users: {result.TotalUsers}");
            Console.WriteLine($"Cache hit ratio: {_queryEngine.GetPerformanceMetrics().CacheHitRatio:P2}");
        }

        [TestMethod]
        public async Task QueryLicenseStatusVerboseAsync_WithRealLmstatOutput_ShouldParseVerboseData()
        {
            // Arrange
            var server = "license-server";
            var port = 27000;
            var lmstatOutput = await File.ReadAllTextAsync(Path.Combine("TestData", "lmstat_verbose_output.txt"));

            _testProcessExecutor.SetMockResult("lmutil.exe", $"-v -c {server}@{port}", new ProcessExecutionResult
            {
                ExitCode = 0,
                Output = lmstatOutput,
                ExecutionTime = TimeSpan.FromMilliseconds(200),
                Success = true
            });

            // Act
            var result = await _queryEngine.QueryLicenseStatusVerboseAsync(server, port);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(server, result.Server);
            Assert.AreEqual(port, result.Port);
            Assert.IsTrue(result.IsServerUp);
            Assert.IsNotNull(result.ServerMessages);
            Assert.IsTrue(result.ServerMessages.Count > 0);

            // Verify verbose-specific information
            Console.WriteLine($"Verbose mode detected {result.ServerMessages.Count} server messages");
            foreach (var message in result.ServerMessages)
            {
                Console.WriteLine($"Server Message: {message}");
            }
        }

        [TestMethod]
        public async Task QueryFeaturesAsync_WithMultipleFeatures_ShouldReturnAllFeatures()
        {
            // Arrange
            var server = "license-server";
            var port = 27000;
            var lmstatOutput = await File.ReadAllTextAsync(Path.Combine("TestData", "lmstat_standard_output.txt"));

            _testProcessExecutor.SetMockResult("lmutil.exe", $"-c {server}@{port}", new ProcessExecutionResult
            {
                ExitCode = 0,
                Output = lmstatOutput,
                ExecutionTime = TimeSpan.FromMilliseconds(120),
                Success = true
            });

            // Act
            var result = await _queryEngine.QueryFeaturesAsync(server, port);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Count >= 3); // solidworks, toolbox, photoview360

            // Verify each feature has correct data
            foreach (var featurePair in result)
            {
                var feature = featurePair.Value;
                Assert.IsNotNull(feature.Name);
                Assert.IsTrue(feature.TotalLicenses > 0);
                Assert.IsTrue(feature.UsedLicenses >= 0);
                Assert.IsTrue(feature.AvailableLicenses >= 0);
                Assert.AreEqual(feature.TotalLicenses, feature.UsedLicenses + feature.AvailableLicenses);

                Console.WriteLine($"Feature: {feature.Name}, Total: {feature.TotalLicenses}, Used: {feature.UsedLicenses}, Available: {feature.AvailableLicenses}");
            }
        }

        [TestMethod]
        public async Task QueryActiveUsersAsync_WithActiveUsers_ShouldReturnActiveUsersOnly()
        {
            // Arrange
            var server = "license-server";
            var port = 27000;
            var lmstatOutput = await File.ReadAllTextAsync(Path.Combine("TestData", "lmstat_standard_output.txt"));

            _testProcessExecutor.SetMockResult("lmutil.exe", $"-c {server}@{port}", new ProcessExecutionResult
            {
                ExitCode = 0,
                Output = lmstatOutput,
                ExecutionTime = TimeSpan.FromMilliseconds(100),
                Success = true
            });

            // Act
            var result = await _queryEngine.QueryActiveUsersAsync(server, port);

            // Assert
            Assert.IsNotNull(result);
            Assert.IsTrue(result.Count > 0);

            var totalActiveUsers = result.Values.Sum(users => users.Count);
            Assert.IsTrue(totalActiveUsers > 0);

            // Verify all returned users are active
            foreach (var featureUsers in result.Values)
            {
                foreach (var user in featureUsers)
                {
                    Assert.AreEqual(LicenseStatus.Active, user.Status);
                    Assert.IsFalse(string.IsNullOrEmpty(user.UserHost));
                    Assert.IsFalse(string.IsNullOrEmpty(user.ClientHost));
                    Assert.IsTrue(user.CheckoutTime > DateTime.MinValue);
                }
            }

            Console.WriteLine($"Found {totalActiveUsers} active users across {result.Count} features");
        }

        [TestMethod]
        public async Task QueryBorrowedLicensesAsync_WithBorrowedLicenses_ShouldReturnBorrowedOnly()
        {
            // Arrange
            var server = "license-server";
            var port = 27000;
            var lmstatOutput = await File.ReadAllTextAsync(Path.Combine("TestData", "lmstat_borrowed_output.txt"));

            _testProcessExecutor.SetMockResult("lmutil.exe", $"-c {server}@{port}", new ProcessExecutionResult
            {
                ExitCode = 0,
                Output = lmstatOutput,
                ExecutionTime = TimeSpan.FromMilliseconds(100),
                Success = true
            });

            // Act
            var result = await _queryEngine.QueryBorrowedLicensesAsync(server, port);

            // Assert
            Assert.IsNotNull(result);

            // Verify all returned licenses are borrowed
            foreach (var license in result)
            {
                Assert.AreEqual(LicenseStatus.Borrowed, license.Status);
                Assert.IsNotNull(license.BorrowTime);
                Assert.IsTrue(license.BorrowTime.Value > DateTime.MinValue);
                Assert.IsFalse(string.IsNullOrEmpty(license.UserHost));
            }

            Console.WriteLine($"Found {result.Count} borrowed licenses");
        }

        [TestMethod]
        public async Task QueryIdleLicensesAsync_WithIdleLicenses_ShouldReturnIdleOnly()
        {
            // Arrange
            var server = "license-server";
            var port = 27000;
            var lmstatOutput = await File.ReadAllTextAsync(Path.Combine("TestData", "lmstat_standard_output.txt"));

            _testProcessExecutor.SetMockResult("lmutil.exe", $"-c {server}@{port}", new ProcessExecutionResult
            {
                ExitCode = 0,
                Output = lmstatOutput,
                ExecutionTime = TimeSpan.FromMilliseconds(100),
                Success = true
            });

            // Act
            var result = await _queryEngine.QueryIdleLicensesAsync(server, port);

            // Assert
            Assert.IsNotNull(result);

            // Verify all returned licenses are idle
            foreach (var license in result)
            {
                Assert.IsTrue(license.IsIdle);
                Assert.AreEqual(LicenseStatus.Idle, license.Status);
                Assert.IsFalse(string.IsNullOrEmpty(license.IdleReason));
            }

            Console.WriteLine($"Found {result.Count} idle licenses");
        }

        [TestMethod]
        public async Task QueryUsageStatisticsAsync_WithValidData_ShouldCalculateCorrectStatistics()
        {
            // Arrange
            var server = "license-server";
            var port = 27000;
            var lmstatOutput = await File.ReadAllTextAsync(Path.Combine("TestData", "lmstat_standard_output.txt"));

            _testProcessExecutor.SetMockResult("lmutil.exe", $"-c {server}@{port}", new ProcessExecutionResult
            {
                ExitCode = 0,
                Output = lmstatOutput,
                ExecutionTime = TimeSpan.FromMilliseconds(100),
                Success = true
            });

            // Act
            var result = await _queryEngine.QueryUsageStatisticsAsync(server, port);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(server, result.Server);
            Assert.AreEqual(port, result.Port);
            Assert.IsTrue(result.TotalLicenses > 0);
            Assert.IsTrue(result.LicensesInUse >= 0);
            Assert.IsTrue(result.AvailableLicenses >= 0);
            Assert.AreEqual(result.TotalLicenses, result.LicensesInUse + result.AvailableLicenses);
            Assert.IsTrue(result.UtilizationPercentage >= 0);
            Assert.IsTrue(result.UtilizationPercentage <= 100);
            Assert.IsTrue(result.AvailabilityPercentage >= 0);
            Assert.IsTrue(result.AvailabilityPercentage <= 100);
            Assert.IsTrue(result.UtilizationPercentage + result.AvailabilityPercentage <= 100.1); // Allow for rounding

            Console.WriteLine($"Usage Statistics: {result}");
        }

        [TestMethod]
        public async Task QueryMultipleServersAsync_WithMultipleServers_ShouldReturnAllResults()
        {
            // Arrange
            var servers = new[] { "server1", "server2", "server3" };
            var ports = new[] { 27000, 27001, 27002 };
            var lmstatOutput = await File.ReadAllTextAsync(Path.Combine("TestData", "lmstat_standard_output.txt"));

            foreach (var server in servers)
            {
                foreach (var port in ports)
                {
                    _testProcessExecutor.SetMockResult("lmutil.exe", $"-c {server}@{port}", new ProcessExecutionResult
                    {
                        ExitCode = 0,
                        Output = lmstatOutput,
                        ExecutionTime = TimeSpan.FromMilliseconds(100),
                        Success = true
                    });
                }
            }

            // Act
            var result = await _queryEngine.QueryMultipleServersAsync(servers, ports);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(servers.Length, result.Count);

            // Verify all servers are present
            for (int i = 0; i < servers.Length; i++)
            {
                var key = $"{servers[i]}:{ports[i]}";
                Assert.IsTrue(result.ContainsKey(key), $"Server {key} not found in results");

                var serverStatus = result[key];
                Assert.AreEqual(servers[i], serverStatus.Server);
                Assert.AreEqual(ports[i], serverStatus.Port);
                Assert.IsTrue(serverStatus.IsServerUp);
            }

            Console.WriteLine($"Successfully queried {result.Count} servers");
        }

        [TestMethod]
        public async Task QueryFeatureAsync_WithSpecificFeature_ShouldReturnFeatureDetails()
        {
            // Arrange
            var server = "license-server";
            var port = 27000;
            var feature = "solidworks";
            var lmstatOutput = await File.ReadAllTextAsync(Path.Combine("TestData", "lmstat_standard_output.txt"));

            _testProcessExecutor.SetMockResult("lmutil.exe", $"-f {feature} -c {server}@{port}", new ProcessExecutionResult
            {
                ExitCode = 0,
                Output = lmstatOutput,
                ExecutionTime = TimeSpan.FromMilliseconds(100),
                Success = true
            });

            // Act
            var result = await _queryEngine.QueryFeatureAsync(server, port, feature);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(feature, result.Name);
            Assert.IsTrue(result.TotalLicenses > 0);
            Assert.IsTrue(result.UsedLicenses >= 0);
            Assert.IsTrue(result.AvailableLicenses >= 0);

            Console.WriteLine($"Feature '{feature}': Total={result.TotalLicenses}, Used={result.UsedLicenses}, Available={result.AvailableLicenses}");
        }

        [TestMethod]
        public async Task CheckServerHealthAsync_WithHealthyServer_ShouldReturnHealthy()
        {
            // Arrange
            var server = "license-server";
            var port = 27000;
            var lmstatOutput = await File.ReadAllTextAsync(Path.Combine("TestData", "lmstat_standard_output.txt"));

            _testProcessExecutor.SetMockResult("lmutil.exe", $"-c {server}@{port}", new ProcessExecutionResult
            {
                ExitCode = 0,
                Output = lmstatOutput,
                ExecutionTime = TimeSpan.FromMilliseconds(50),
                Success = true
            });

            // Act
            var result = await _queryEngine.CheckServerHealthAsync(server, port);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(server, result.Server);
            Assert.AreEqual(port, result.Port);
            Assert.IsTrue(result.IsHealthy);
            Assert.IsTrue(result.ResponseTimeMs > 0);
            Assert.IsTrue(result.ResponseTimeMs < 1000); // Should be reasonable
            Assert.IsNotNull(result.Message);
            Assert.IsTrue(string.IsNullOrEmpty(result.ErrorMessage));

            Console.WriteLine($"Server health check: {result}");
        }

        [TestMethod]
        public async Task CheckServerHealthAsync_WithUnhealthyServer_ShouldReturnUnhealthy()
        {
            // Arrange
            var server = "license-server";
            var port = 27000;

            _testProcessExecutor.SetMockResult("lmutil.exe", $"-c {server}@{port}", new ProcessExecutionResult
            {
                ExitCode = 1,
                Error = "Connection refused",
                ExecutionTime = TimeSpan.FromMilliseconds(5000),
                Success = false
            });

            // Act
            var result = await _queryEngine.CheckServerHealthAsync(server, port);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(server, result.Server);
            Assert.AreEqual(port, result.Port);
            Assert.IsFalse(result.IsHealthy);
            Assert.IsTrue(result.ResponseTimeMs > 0);
            Assert.AreEqual("Health check failed", result.Message);
            Assert.IsFalse(string.IsNullOrEmpty(result.ErrorMessage));

            Console.WriteLine($"Server health check (unhealthy): {result}");
        }

        [TestMethod]
        public async Task CachingIntegration_WithValidCache_ShouldUseCachedData()
        {
            // Arrange
            var server = "license-server";
            var port = 27000;
            var lmstatOutput = await File.ReadAllTextAsync(Path.Combine("TestData", "lmstat_standard_output.txt"));

            // First call - should execute lmstat
            _testProcessExecutor.SetMockResult("lmutil.exe", $"-c {server}@{port}", new ProcessExecutionResult
            {
                ExitCode = 0,
                Output = lmstatOutput,
                ExecutionTime = TimeSpan.FromMilliseconds(100),
                Success = true
            });

            // Act - First call
            var result1 = await _queryEngine.QueryLicenseStatusAsync(server, port);
            var firstCallMetrics = _queryEngine.GetPerformanceMetrics();

            // Act - Second call (should use cache)
            var result2 = await _queryEngine.QueryLicenseStatusAsync(server, port);
            var secondCallMetrics = _queryEngine.GetPerformanceMetrics();

            // Assert
            Assert.IsNotNull(result1);
            Assert.IsNotNull(result2);
            Assert.AreEqual(result1.Server, result2.Server);
            Assert.AreEqual(result1.Port, result2.Port);

            // Verify caching behavior
            Assert.IsTrue(secondCallMetrics.CachedQueries > firstCallMetrics.CachedQueries);
            Assert.IsTrue(secondCallMetrics.CacheHitRatio > firstCallMetrics.CacheHitRatio);

            // Verify lmstat was only called once
            Assert.AreEqual(1, _testProcessExecutor.GetCallCount("lmutil.exe", $"-c {server}@{port}"));

            Console.WriteLine($"Cache hit ratio improved from {firstCallMetrics.CacheHitRatio:P2} to {secondCallMetrics.CacheHitRatio:P2}");
        }

        [TestMethod]
        public async Task ErrorRecovery_WithTransientError_ShouldRetryAndSucceed()
        {
            // Arrange
            var server = "license-server";
            var port = 27000;
            var lmstatOutput = await File.ReadAllTextAsync(Path.Combine("TestData", "lmstat_standard_output.txt"));

            var callCount = 0;
            _testProcessExecutor.SetMockResult("lmutil.exe", $"-c {server}@{port}", () =>
            {
                callCount++;
                if (callCount == 1)
                {
                    // First call fails
                    return new ProcessExecutionResult
                    {
                        ExitCode = -1,
                        Error = "Network timeout",
                        ExecutionTime = TimeSpan.FromMilliseconds(5000),
                        Success = false
                    };
                }
                else
                {
                    // Second call succeeds
                    return new ProcessExecutionResult
                    {
                        ExitCode = 0,
                        Output = lmstatOutput,
                        ExecutionTime = TimeSpan.FromMilliseconds(100),
                        Success = true
                    };
                }
            });

            _queryEngine.Options.MaxRetries = 2;
            _queryEngine.Options.RetryDelay = TimeSpan.FromMilliseconds(10);

            // Act
            var result = await _queryEngine.QueryLicenseStatusAsync(server, port);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(server, result.Server);
            Assert.AreEqual(port, result.Port);
            Assert.IsTrue(result.IsServerUp);
            Assert.AreEqual(2, callCount); // Should have retried once

            Console.WriteLine($"Successfully recovered after {callCount - 1} retries");
        }

        [TestMethod]
        public async Task PerformanceMetrics_WithMultipleQueries_ShouldTrackCorrectly()
        {
            // Arrange
            var server = "license-server";
            var port = 27000;
            var lmstatOutput = await File.ReadAllTextAsync(Path.Combine("TestData", "lmstat_standard_output.txt"));

            _testProcessExecutor.SetMockResult("lmutil.exe", $"-c {server}@{port}", new ProcessExecutionResult
            {
                ExitCode = 0,
                Output = lmstatOutput,
                ExecutionTime = TimeSpan.FromMilliseconds(100),
                Success = true
            });

            // Reset metrics
            _queryEngine.ResetPerformanceMetrics();

            // Act
            var tasks = new List<Task<LicenseServerStatus>>();
            for (int i = 0; i < 10; i++)
            {
                tasks.Add(_queryEngine.QueryLicenseStatusAsync(server, port));
            }
            await Task.WhenAll(tasks);

            var metrics = _queryEngine.GetPerformanceMetrics();

            // Assert
            Assert.AreEqual(10, metrics.TotalQueries);
            Assert.AreEqual(10, metrics.SuccessfulQueries);
            Assert.AreEqual(0, metrics.FailedQueries);
            Assert.IsTrue(metrics.AverageQueryTimeMs > 0);
            Assert.IsTrue(metrics.MinQueryTimeMs > 0);
            Assert.IsTrue(metrics.MaxQueryTimeMs >= metrics.MinQueryTimeMs);
            Assert.AreEqual(0, metrics.CachedQueries); // No caching in this test
            Assert.AreEqual(0, metrics.CacheHitRatio);

            Console.WriteLine($"Performance Metrics: {metrics}");
        }

        [TestMethod]
        public async Task QueryUsersAsync_WithSpecificUsers_ShouldReturnFilteredResults()
        {
            // Arrange
            var server = "license-server";
            var port = 27000;
            var lmstatOutput = await File.ReadAllTextAsync(Path.Combine("TestData", "lmstat_standard_output.txt"));
            var targetUsers = new[] { "johndoe", "alice" };

            _testProcessExecutor.SetMockResult("lmutil.exe", $"-c {server}@{port}", new ProcessExecutionResult
            {
                ExitCode = 0,
                Output = lmstatOutput,
                ExecutionTime = TimeSpan.FromMilliseconds(100),
                Success = true
            });

            // Act
            var result = await _queryEngine.QueryUsersAsync(server, port, targetUsers);

            // Assert
            Assert.IsNotNull(result);

            // Verify only target users are returned
            foreach (var user in targetUsers)
            {
                if (result.ContainsKey(user))
                {
                    var userLicenses = result[user];
                    foreach (var license in userLicenses)
                    {
                        Assert.AreEqual(user, license.UserHost, "Returned user should match requested user");
                    }
                    Console.WriteLine($"User {user} has {userLicenses.Count} licenses");
                }
            }

            // Verify no unexpected users are returned
            foreach (var returnedUser in result.Keys)
            {
                Assert.IsTrue(targetUsers.Contains(returnedUser, StringComparer.OrdinalIgnoreCase),
                    $"Unexpected user {returnedUser} returned in results");
            }
        }

        [TestMethod]
        public async Task ConfigurationValidation_WithInvalidConfiguration_ShouldReturnErrors()
        {
            // Arrange - Create engine with invalid dependencies
            var invalidEngine = new LicenseQueryEngine(null, null, null);

            // Act
            var errors = invalidEngine.ValidateConfiguration();

            // Assert
            Assert.IsNotNull(errors);
            Assert.IsTrue(errors.Count > 0);
            Assert.IsTrue(errors.Any(e => e.Contains("Cache manager")));
            Assert.IsTrue(errors.Any(e => e.Contains("Process executor")));
            Assert.IsTrue(errors.Any(e => e.Contains("Output parser")));

            Console.WriteLine($"Configuration validation found {errors.Count} errors:");
            foreach (var error in errors)
            {
                Console.WriteLine($"  - {error}");
            }
        }

        [TestMethod]
        public async Task CacheInvalidation_WithInvalidationCalls_ShouldClearCache()
        {
            // Arrange
            var server = "license-server";
            var port = 27000;
            var feature = "solidworks";
            var lmstatOutput = await File.ReadAllTextAsync(Path.Combine("TestData", "lmstat_standard_output.txt"));

            _testProcessExecutor.SetMockResult("lmutil.exe", $"-c {server}@{port}", new ProcessExecutionResult
            {
                ExitCode = 0,
                Output = lmstatOutput,
                ExecutionTime = TimeSpan.FromMilliseconds(100),
                Success = true
            });

            // Prime cache
            await _queryEngine.QueryLicenseStatusAsync(server, port);
            await _queryEngine.QueryFeatureAsync(server, port, feature);

            var metricsBefore = _queryEngine.GetPerformanceMetrics();

            // Act
            await _queryEngine.InvalidateServerCacheAsync(server, port);
            await _queryEngine.InvalidateFeatureCacheAsync(server, port, feature);
            await _queryEngine.ClearAllCacheAsync();

            var metricsAfter = _queryEngine.GetPerformanceMetrics();

            // Assert
            // Verify cache operations completed successfully
            Assert.IsTrue(_testCacheManager.WasServerInvalidated(server, port));
            Assert.IsTrue(_testCacheManager.WasFeatureInvalidated(server, port, feature));
            Assert.IsTrue(_testCacheManager.WasCleared());

            Console.WriteLine($"Cache invalidation completed. Metrics before: {metricsBefore.CachedQueries} cached, after: {metricsAfter.CachedQueries} cached");
        }

        [TestMethod]
        public async Task EndToEndWorkflow_WithCompleteScenario_ShouldHandleAllOperations()
        {
            // Arrange
            var server = "license-server";
            var port = 27000;
            var lmstatOutput = await File.ReadAllTextAsync(Path.Combine("TestData", "lmstat_standard_output.txt"));

            _testProcessExecutor.SetMockResult("lmutil.exe", $"-c {server}@{port}", new ProcessExecutionResult
            {
                ExitCode = 0,
                Output = lmstatOutput,
                ExecutionTime = TimeSpan.FromMilliseconds(100),
                Success = true
            });

            var stopwatch = Stopwatch.StartNew();
            _queryEngine.ResetPerformanceMetrics();

            // Act - Complete workflow
            var healthCheck = await _queryEngine.CheckServerHealthAsync(server, port);
            var serverStatus = await _queryEngine.QueryLicenseStatusAsync(server, port);
            var features = await _queryEngine.QueryFeaturesAsync(server, port);
            var activeUsers = await _queryEngine.QueryActiveUsersAsync(server, port);
            var statistics = await _queryEngine.QueryUsageStatisticsAsync(server, port);
            var finalMetrics = _queryEngine.GetPerformanceMetrics();

            stopwatch.Stop();

            // Assert
            Assert.IsNotNull(healthCheck);
            Assert.IsTrue(healthCheck.IsHealthy);

            Assert.IsNotNull(serverStatus);
            Assert.IsTrue(serverStatus.IsServerUp);

            Assert.IsNotNull(features);
            Assert.IsTrue(features.Count > 0);

            Assert.IsNotNull(activeUsers);
            Assert.IsTrue(activeUsers.Values.Sum(users => users.Count) > 0);

            Assert.IsNotNull(statistics);
            Assert.IsTrue(statistics.TotalLicenses > 0);

            Assert.IsNotNull(finalMetrics);
            Assert.AreEqual(4, finalMetrics.TotalQueries); // health, status, features, statistics (active users uses features)
            Assert.AreEqual(4, finalMetrics.SuccessfulQueries);
            Assert.AreEqual(0, finalMetrics.FailedQueries);

            Console.WriteLine($"End-to-end workflow completed in {stopwatch.ElapsedMilliseconds}ms");
            Console.WriteLine($"Final metrics: {finalMetrics}");
            Console.WriteLine($"Server health: {healthCheck.IsHealthy}, Features: {features.Count}, Active users: {activeUsers.Values.Sum(users => users.Count)}");
        }
    }

    /// <summary>
    /// Test implementation of IProcessExecutor for integration testing
    /// </summary>
    internal class TestProcessExecutor : IProcessExecutor
    {
        private readonly Dictionary<string, ProcessExecutionResult> _mockResults = new Dictionary<string, ProcessExecutionResult>();
        private readonly Dictionary<string, int> _callCounts = new Dictionary<string, int>();
        private readonly Dictionary<string, Func<ProcessExecutionResult>> _dynamicResults = new Dictionary<string, Func<ProcessExecutionResult>>();

        public void SetMockResult(string fileName, string arguments, ProcessExecutionResult result)
        {
            var key = $"{fileName}|{arguments}";
            _mockResults[key] = result;
        }

        public void SetMockResult(string fileName, string arguments, Func<ProcessExecutionResult> resultFactory)
        {
            var key = $"{fileName}|{arguments}";
            _dynamicResults[key] = resultFactory;
        }

        public int GetCallCount(string fileName, string arguments)
        {
            var key = $"{fileName}|{arguments}";
            return _callCounts.TryGetValue(key, out var count) ? count : 0;
        }

        public int GetTotalCallCount()
        {
            return _callCounts.Values.Sum();
        }

        public void Reset()
        {
            _mockResults.Clear();
            _callCounts.Clear();
            _dynamicResults.Clear();
        }

        public Task<ProcessExecutionResult> ExecuteAsync(string fileName, string arguments, TimeSpan timeout, CancellationToken cancellationToken = default)
        {
            var key = $"{fileName}|{arguments}";

            // Track call count
            if (!_callCounts.ContainsKey(key))
            {
                _callCounts[key] = 0;
            }
            _callCounts[key]++;

            // Return result
            if (_dynamicResults.TryGetValue(key, out var dynamicResult))
            {
                return Task.FromResult(dynamicResult());
            }

            if (_mockResults.TryGetValue(key, out var staticResult))
            {
                return Task.FromResult(staticResult);
            }

            // Default failure result
            return Task.FromResult(new ProcessExecutionResult
            {
                ExitCode = -1,
                Error = $"No mock result configured for {key}",
                ExecutionTime = TimeSpan.FromMilliseconds(10),
                Success = false
            });
        }
    }

    /// <summary>
    /// Test implementation of ICacheManager for integration testing
    /// </summary>
    internal class TestCacheManager : ICacheManager
    {
        private readonly Dictionary<string, object> _cache = new Dictionary<string, object>();
        private readonly Dictionary<string, DateTime> _expirationTimes = new Dictionary<string, DateTime>();
        private readonly HashSet<string> _invalidatedServers = new HashSet<string>();
        private readonly HashSet<string> _invalidatedFeatures = new HashSet<string>();
        private bool _wasCleared = false;

        public CacheOptions Options { get; set; } = new CacheOptions();
        public CacheStatistics Statistics { get; } = new CacheStatistics();

        public T Get<T>(string key)
        {
            if (_expirationTimes.TryGetValue(key, out var expiration) && expiration <= DateTime.Now)
            {
                Remove(key);
                return default;
            }

            return _cache.TryGetValue(key, out var value) ? (T)value : default;
        }

        public async Task<T> GetAsync<T>(string key, CancellationToken cancellationToken = default)
        {
            return await Task.FromResult(Get<T>(key));
        }

        public void Set<T>(string key, T value)
        {
            _cache[key] = value;
        }

        public void Set<T>(string key, T value, TimeSpan expiration)
        {
            _cache[key] = value;
            _expirationTimes[key] = DateTime.Now.Add(expiration);
        }

        public async Task SetAsync<T>(string key, T value, CancellationToken cancellationToken = default)
        {
            await Task.Run(() => Set(key, value), cancellationToken);
        }

        public async Task SetAsync<T>(string key, T value, TimeSpan expiration, CancellationToken cancellationToken = default)
        {
            await Task.Run(() => Set(key, value, expiration), cancellationToken);
        }

        public bool Remove(string key)
        {
            var removed = _cache.Remove(key);
            _expirationTimes.Remove(key);
            return removed;
        }

        public async Task<bool> RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            return await Task.FromResult(Remove(key));
        }

        public bool Contains(string key)
        {
            if (_expirationTimes.TryGetValue(key, out var expiration) && expiration <= DateTime.Now)
            {
                Remove(key);
                return false;
            }
            return _cache.ContainsKey(key);
        }

        public async Task<bool> ContainsAsync(string key, CancellationToken cancellationToken = default)
        {
            return await Task.FromResult(Contains(key));
        }

        public T GetOrCreate<T>(string key, Func<T> factory, TimeSpan? expiration = null)
        {
            if (Contains(key))
            {
                return Get<T>(key);
            }

            var value = factory();
            if (expiration.HasValue)
            {
                Set(key, value, expiration.Value);
            }
            else
            {
                Set(key, value);
            }
            return value;
        }

        public async Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> factory, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
        {
            if (Contains(key))
            {
                return await Task.FromResult(Get<T>(key));
            }

            var value = await factory();
            if (expiration.HasValue)
            {
                await SetAsync(key, value, expiration.Value, cancellationToken);
            }
            else
            {
                await SetAsync(key, value, cancellationToken);
            }
            return value;
        }

        public Dictionary<string, LicenseInfo> GetLicenseInfo(string server, int port)
        {
            var key = GenerateLicenseInfoKey(server, port);
            return Get<Dictionary<string, LicenseInfo>>(key);
        }

        public async Task<Dictionary<string, LicenseInfo>> GetLicenseInfoAsync(string server, int port, CancellationToken cancellationToken = default)
        {
            return await GetAsync<Dictionary<string, LicenseInfo>>(GenerateLicenseInfoKey(server, port), cancellationToken);
        }

        public void SetLicenseInfo(string server, int port, Dictionary<string, LicenseInfo> licenseInfo, TimeSpan? expiration = null)
        {
            var key = GenerateLicenseInfoKey(server, port);
            if (expiration.HasValue)
            {
                Set(key, licenseInfo, expiration.Value);
            }
            else
            {
                Set(key, licenseInfo);
            }
        }

        public async Task SetLicenseInfoAsync(string server, int port, Dictionary<string, LicenseInfo> licenseInfo, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
        {
            var key = GenerateLicenseInfoKey(server, port);
            if (expiration.HasValue)
            {
                await SetAsync(key, licenseInfo, expiration.Value, cancellationToken);
            }
            else
            {
                await SetAsync(key, licenseInfo, cancellationToken);
            }
        }

        public LicenseFeature GetLicenseFeature(string server, int port, string feature)
        {
            var key = GenerateLicenseFeatureKey(server, port, feature);
            return Get<LicenseFeature>(key);
        }

        public async Task<LicenseFeature> GetLicenseFeatureAsync(string server, int port, string feature, CancellationToken cancellationToken = default)
        {
            return await GetAsync<LicenseFeature>(GenerateLicenseFeatureKey(server, port, feature), cancellationToken);
        }

        public void SetLicenseFeature(string server, int port, string feature, LicenseFeature licenseFeature, TimeSpan? expiration = null)
        {
            var key = GenerateLicenseFeatureKey(server, port, feature);
            if (expiration.HasValue)
            {
                Set(key, licenseFeature, expiration.Value);
            }
            else
            {
                Set(key, licenseFeature);
            }
        }

        public async Task SetLicenseFeatureAsync(string server, int port, string feature, LicenseFeature licenseFeature, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
        {
            var key = GenerateLicenseFeatureKey(server, port, feature);
            if (expiration.HasValue)
            {
                await SetAsync(key, licenseFeature, expiration.Value, cancellationToken);
            }
            else
            {
                await SetAsync(key, licenseFeature, cancellationToken);
            }
        }

        public LicenseServerStatus GetServerStatus(string server, int port)
        {
            var key = GenerateServerStatusKey(server, port);
            return Get<LicenseServerStatus>(key);
        }

        public async Task<LicenseServerStatus> GetServerStatusAsync(string server, int port, CancellationToken cancellationToken = default)
        {
            return await GetAsync<LicenseServerStatus>(GenerateServerStatusKey(server, port), cancellationToken);
        }

        public void SetServerStatus(string server, int port, LicenseServerStatus serverStatus, TimeSpan? expiration = null)
        {
            var key = GenerateServerStatusKey(server, port);
            if (expiration.HasValue)
            {
                Set(key, serverStatus, expiration.Value);
            }
            else
            {
                Set(key, serverStatus);
            }
        }

        public async Task SetServerStatusAsync(string server, int port, LicenseServerStatus serverStatus, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
        {
            var key = GenerateServerStatusKey(server, port);
            if (expiration.HasValue)
            {
                await SetAsync(key, serverStatus, expiration.Value, cancellationToken);
            }
            else
            {
                await SetAsync(key, serverStatus, cancellationToken);
            }
        }

        public void Clear()
        {
            _cache.Clear();
            _expirationTimes.Clear();
            _wasCleared = true;
        }

        public async Task ClearAsync(CancellationToken cancellationToken = default)
        {
            await Task.Run(Clear, cancellationToken);
        }

        public int RemoveExpired()
        {
            var now = DateTime.Now;
            var expiredKeys = _expirationTimes.Where(kvp => kvp.Value <= now).Select(kvp => kvp.Key).ToList();
            var count = 0;

            foreach (var key in expiredKeys)
            {
                if (Remove(key))
                {
                    count++;
                }
            }

            return count;
        }

        public async Task<int> RemoveExpiredAsync(CancellationToken cancellationToken = default)
        {
            return await Task.FromResult(RemoveExpired());
        }

        public string GenerateLicenseInfoKey(string server, int port) => $"license_info_{server}_{port}";
        public string GenerateLicenseFeatureKey(string server, int port, string feature) => $"license_feature_{server}_{port}_{feature}";
        public string GenerateServerStatusKey(string server, int port) => $"server_status_{server}_{port}";

        public void InvalidateServer(string server, int port)
        {
            var serverKey = $"{server}:{port}";
            _invalidatedServers.Add(serverKey);
        }

        public async Task InvalidateServerAsync(string server, int port, CancellationToken cancellationToken = default)
        {
            await Task.Run(() => InvalidateServer(server, port), cancellationToken);
        }

        public void InvalidateFeature(string server, int port, string feature)
        {
            var featureKey = $"{server}:{port}:{feature}";
            _invalidatedFeatures.Add(featureKey);
        }

        public async Task InvalidateFeatureAsync(string server, int port, string feature, CancellationToken cancellationToken = default)
        {
            await Task.Run(() => InvalidateFeature(server, port, feature), cancellationToken);
        }

        public bool WasServerInvalidated(string server, int port)
        {
            var serverKey = $"{server}:{port}";
            return _invalidatedServers.Contains(serverKey);
        }

        public bool WasFeatureInvalidated(string server, int port, string feature)
        {
            var featureKey = $"{server}:{port}:{feature}";
            return _invalidatedFeatures.Contains(featureKey);
        }

        public bool WasCleared()
        {
            return _wasCleared;
        }
    }
}