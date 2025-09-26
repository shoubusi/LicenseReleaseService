using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
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
    /// Tests for error scenarios and recovery mechanisms of the LicenseQueryEngine
    /// </summary>
    [TestClass]
    public class ErrorScenarioAndRecoveryTests
    {
        private ICacheManager _cacheManager;
        private IProcessExecutor _processExecutor;
        private LmstatOutputParser _outputParser;
        private LicenseQueryEngine _queryEngine;
        private TestProcessExecutor _testProcessExecutor;
        private TestCacheManager _testCacheManager;
        private string _lmstatOutput;
        private string _malformedOutput;
        private string _errorOutput;

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
            _queryEngine.Options.MaxRetries = 3;
            _queryEngine.Options.RetryDelay = TimeSpan.FromMilliseconds(10);
            _queryEngine.Options.EnableErrorRecovery = true;

            // Load test data
            _lmstatOutput = await File.ReadAllTextAsync(Path.Combine("TestData", "lmstat_standard_output.txt"));
            _malformedOutput = await File.ReadAllTextAsync(Path.Combine("TestData", "lmstat_malformed_output.txt"));
            _errorOutput = await File.ReadAllTextAsync(Path.Combine("TestData", "lmstat_error_output.txt"));

            // Set up default mock result
            SetupDefaultMockResults();
        }

        [TestCleanup]
        public void TestCleanup()
        {
            _queryEngine?.ClearAllCacheAsync().GetAwaiter().GetResult();
            _testCacheManager?.Clear();
            _testProcessExecutor?.Reset();
        }

        [TestMethod]
        public async Task NetworkTimeoutError_ShouldRetryAndSucceed()
        {
            // Arrange
            var server = "timeout-server";
            var port = 27000;
            var attempts = 0;

            _testProcessExecutor.SetMockResult("lmutil.exe", $"-c {server}@{port}", () =>
            {
                attempts++;
                if (attempts <= 2)
                {
                    // First two attempts timeout
                    throw new TimeoutException("Connection timeout");
                }
                else
                {
                    // Third attempt succeeds
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
            var result = await _queryEngine.QueryLicenseStatusAsync(server, port);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(server, result.Server);
            Assert.AreEqual(port, result.Port);
            Assert.IsTrue(result.IsServerUp);
            Assert.AreEqual(3, attempts); // Should have retried twice

            Console.WriteLine($"Network timeout recovery: Succeeded after {attempts - 1} retries");
        }

        [TestMethod]
        public async Task ConnectionRefusedError_ShouldRetryAndSucceed()
        {
            // Arrange
            var server = "refused-server";
            var port = 27000;
            var attempts = 0;

            _testProcessExecutor.SetMockResult("lmutil.exe", $"-c {server}@{port}", () =>
            {
                attempts++;
                if (attempts == 1)
                {
                    throw new SocketException((int)SocketError.ConnectionRefused);
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
            var result = await _queryEngine.QueryLicenseStatusAsync(server, port);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(server, result.Server);
            Assert.AreEqual(port, result.Port);
            Assert.IsTrue(result.IsServerUp);
            Assert.AreEqual(2, attempts); // Should have retried once

            Console.WriteLine($"Connection refused recovery: Succeeded after {attempts - 1} retries");
        }

        [TestMethod]
        public async Task ProcessExecutionError_ShouldHandleNonRetryableErrors()
        {
            // Arrange
            var server = "execution-error-server";
            var port = 27000;

            _testProcessExecutor.SetMockResult("lmutil.exe", $"-c {server}@{port}", new ProcessExecutionResult
            {
                ExitCode = 127, // Command not found
                Error = "lmutil.exe: command not found",
                ExecutionTime = TimeSpan.FromMilliseconds(10),
                Success = false
            });

            // Act & Assert
            var exception = await Assert.ThrowsExceptionAsync<LicenseQueryException>(
                () => _queryEngine.QueryLicenseStatusAsync(server, port));

            Assert.AreEqual("QueryLicenseStatusAsync", exception.QueryType);
            Assert.AreEqual(server, exception.Server);
            Assert.AreEqual(port, exception.Port);
            Assert.AreEqual(LicenseQueryErrorCode.ProcessExecutionError, exception.ErrorCode);
            Assert.IsFalse(exception.IsTransient);
            Assert.Contains("command not found", exception.Message);

            Console.WriteLine($"Process execution error: {exception.Message}");
        }

        [TestMethod]
        public async Task MalformedOutputError_ShouldHandleGracefully()
        {
            // Arrange
            var server = "malformed-server";
            var port = 27000;

            _testProcessExecutor.SetMockResult("lmutil.exe", $"-c {server}@{port}", new ProcessExecutionResult
            {
                ExitCode = 0,
                Output = _malformedOutput,
                ExecutionTime = TimeSpan.FromMilliseconds(50),
                Success = true
            });

            // Act
            var result = await _queryEngine.QueryLicenseStatusAsync(server, port);

            // Assert
            // Should still return a valid result even with malformed output
            Assert.IsNotNull(result);
            Assert.AreEqual(server, result.Server);
            Assert.AreEqual(port, result.Port);
            // The parser should handle malformed output gracefully
            // May not have full feature details, but should not crash

            Console.WriteLine($"Malformed output handling: Server={result.Server}, Status={result.IsServerUp}");
            Console.WriteLine($"Features found: {result.FeatureDetails?.Count ?? 0}");
        }

        [TestMethod]
        public async Task ServerNotFoundError_ShouldNotRetry()
        {
            // Arrange
            var server = "nonexistent-server";
            var port = 27000;
            var attempts = 0;

            _testProcessExecutor.SetMockResult("lmutil.exe", $"-c {server}@{port}", () =>
            {
                attempts++;
                throw new LicenseQueryException(
                    "Server not found",
                    server,
                    port,
                    "QueryLicenseStatusAsync",
                    LicenseQueryErrorCode.ServerNotFound,
                    false); // Non-transient error
            });

            // Act & Assert
            var exception = await Assert.ThrowsExceptionAsync<LicenseQueryException>(
                () => _queryEngine.QueryLicenseStatusAsync(server, port));

            Assert.AreEqual(LicenseQueryErrorCode.ServerNotFound, exception.ErrorCode);
            Assert.IsFalse(exception.IsTransient);
            Assert.AreEqual(1, attempts); // Should not retry non-transient errors

            Console.WriteLine($"Server not found error: {exception.Message} (retries: {attempts - 1})");
        }

        [TestMethod]
        public async Task CircuitBreaker_WithMultipleFailures_ShouldOpenCircuit()
        {
            // Arrange
            var server = "circuit-test-server";
            var port = 27000;
            var failureCount = 0;

            _testProcessExecutor.SetMockResult("lmutil.exe", $"-c {server}@{port}", () =>
            {
                failureCount++;
                throw new TimeoutException("Simulated timeout");
            });

            // Act - trigger multiple failures to open circuit
            LicenseQueryException lastException = null;
            for (int i = 0; i < 5; i++)
            {
                try
                {
                    await _queryEngine.QueryLicenseStatusAsync(server, port);
                }
                catch (LicenseQueryException ex)
                {
                    lastException = ex;
                }
            }

            // Assert
            Assert.IsNotNull(lastException);
            Assert.IsTrue(failureCount <= 4, $"Circuit breaker should have opened after ~4 failures, but got {failureCount} attempts");

            Console.WriteLine($"Circuit breaker test: Failed after {failureCount} attempts");
            Console.WriteLine($"Last exception: {lastException?.Message}");
        }

        [TestMethod]
        public async Task PartialFailure_WithSomeServersWorking_ShouldReturnPartialResults()
        {
            // Arrange
            var servers = new[] { "working-server", "failing-server", "working-server2" };
            var ports = new[] { 27000, 27001, 27002 };

            // Set up mixed results
            _testProcessExecutor.SetMockResult("lmutil.exe", "-c working-server@27000", new ProcessExecutionResult
            {
                ExitCode = 0,
                Output = _lmstatOutput,
                ExecutionTime = TimeSpan.FromMilliseconds(50),
                Success = true
            });

            _testProcessExecutor.SetMockResult("lmutil.exe", "-c failing-server@27001", new ProcessExecutionResult
            {
                ExitCode = 1,
                Error = "Connection refused",
                ExecutionTime = TimeSpan.FromMilliseconds(1000),
                Success = false
            });

            _testProcessExecutor.SetMockResult("lmutil.exe", "-c working-server2@27002", new ProcessExecutionResult
            {
                ExitCode = 0,
                Output = _lmstatOutput,
                ExecutionTime = TimeSpan.FromMilliseconds(50),
                Success = true
            });

            // Act
            var results = new Dictionary<string, LicenseServerStatus>();
            var exceptions = new List<Exception>();

            foreach (var (server, port) in servers.Zip(ports, (s, p) => (s, p)))
            {
                try
                {
                    var result = await _queryEngine.QueryLicenseStatusAsync(server, port);
                    results[$"{server}:{port}"] = result;
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            }

            // Assert
            Assert.AreEqual(2, results.Count, "Should have results from working servers");
            Assert.AreEqual(1, exceptions.Count, "Should have one exception from failing server");

            // Verify working servers returned valid results
            Assert.IsTrue(results.ContainsKey("working-server:27000"));
            Assert.IsTrue(results.ContainsKey("working-server2:27002"));
            Assert.IsTrue(results["working-server:27000"].IsServerUp);
            Assert.IsTrue(results["working-server2:27002"].IsServerUp);

            // Verify the exception is from the failing server
            var failedException = exceptions[0] as LicenseQueryException;
            Assert.IsNotNull(failedException);
            Assert.AreEqual("failing-server", failedException.Server);

            Console.WriteLine($"Partial failure results: {results.Count} successful, {exceptions.Count} failed");
            Console.WriteLine($"Successful servers: {string.Join(", ", results.Keys)}");
            Console.WriteLine($"Failed server: {failedException?.Server}:{failedException?.Port}");
        }

        [TestMethod]
        public async Task CacheFallback_WithServerFailure_ShouldUseCachedData()
        {
            // Arrange
            var server = "cache-fallback-server";
            var port = 27000;

            // First, populate cache with good data
            _testProcessExecutor.SetMockResult("lmutil.exe", $"-c {server}@{port}", new ProcessExecutionResult
            {
                ExitCode = 0,
                Output = _lmstatOutput,
                ExecutionTime = TimeSpan.FromMilliseconds(50),
                Success = true
            });

            var initialResult = await _queryEngine.QueryLicenseStatusAsync(server, port);
            Assert.IsNotNull(initialResult);
            Assert.IsTrue(initialResult.IsServerUp);

            // Now set up the server to fail
            _testProcessExecutor.SetMockResult("lmutil.exe", $"-c {server}@{port}", new ProcessExecutionResult
            {
                ExitCode = 1,
                Error = "Server temporarily unavailable",
                ExecutionTime = TimeSpan.FromMilliseconds(1000),
                Success = false
            });

            // Act - should still work due to cache
            var cachedResult = await _queryEngine.QueryLicenseStatusAsync(server, port);

            // Assert
            Assert.IsNotNull(cachedResult);
            Assert.AreEqual(server, cachedResult.Server);
            Assert.AreEqual(port, cachedResult.Port);
            // Result should come from cache, so should match initial result
            Assert.AreEqual(initialResult.IsServerUp, cachedResult.IsServerUp);

            Console.WriteLine($"Cache fallback: Returned cached data when server failed");
            Console.WriteLine($"Initial result: {initialResult.IsServerUp}, Cached result: {cachedResult.IsServerUp}");
        }

        [TestMethod]
        public async Task SlowResponse_WithTimeout_ShouldThrowTimeoutException()
        {
            // Arrange
            var server = "slow-server";
            var port = 27000;

            _queryEngine.Options.QueryTimeout = TimeSpan.FromMilliseconds(100);

            _testProcessExecutor.SetMockResult("lmutil.exe", $"-c {server}@{port}", new ProcessExecutionResult
            {
                ExitCode = 0,
                Output = _lmstatOutput,
                ExecutionTime = TimeSpan.FromMilliseconds(500), // Slower than timeout
                Success = true
            });

            // Act & Assert
            var exception = await Assert.ThrowsExceptionAsync<LicenseQueryException>(
                () => _queryEngine.QueryLicenseStatusAsync(server, port));

            Assert.AreEqual(LicenseQueryErrorCode.NetworkTimeout, exception.ErrorCode);
            Assert.IsTrue(exception.IsTransient);
            Assert.Contains("timeout", exception.Message.ToLower());

            Console.WriteLine($"Slow response timeout: {exception.Message}");
        }

        [TestMethod]
        public async Task AuthenticationError_ShouldNotRetry()
        {
            // Arrange
            var server = "auth-server";
            var port = 27000;
            var attempts = 0;

            _testProcessExecutor.SetMockResult("lmutil.exe", $"-c {server}@{port}", () =>
            {
                attempts++;
                return new ProcessExecutionResult
                {
                    ExitCode = 5, // Authentication error
                    Error = "Authentication failed: Invalid credentials",
                    ExecutionTime = TimeSpan.FromMilliseconds(100),
                    Success = false
                };
            });

            // Act & Assert
            var exception = await Assert.ThrowsExceptionAsync<LicenseQueryException>(
                () => _queryEngine.QueryLicenseStatusAsync(server, port));

            Assert.AreEqual(LicenseQueryErrorCode.ProcessExecutionError, exception.ErrorCode);
            Assert.IsFalse(exception.IsTransient); // Authentication errors are not retryable
            Assert.AreEqual(1, attempts); // Should not retry authentication errors

            Console.WriteLine($"Authentication error: {exception.Message} (retries: {attempts - 1})");
        }

        [TestMethod]
        public async Task ResourceExhaustion_WithHighLoad_ShouldRecover()
        {
            // Arrange
            var server = "resource-exhaustion-server";
            var port = 27000;
            var requestCount = 0;

            _testProcessExecutor.SetMockResult("lmutil.exe", $"-c {server}@{port}", () =>
            {
                requestCount++;
                if (requestCount <= 5)
                {
                    // First 5 requests fail with resource exhaustion
                    return new ProcessExecutionResult
                    {
                        ExitCode = -1,
                        Error = "Resource temporarily unavailable",
                        ExecutionTime = TimeSpan.FromMilliseconds(100),
                        Success = false
                    };
                }
                else
                {
                    // Subsequent requests succeed
                    return new ProcessExecutionResult
                    {
                        ExitCode = 0,
                        Output = _lmstatOutput,
                        ExecutionTime = TimeSpan.FromMilliseconds(50),
                        Success = true
                    };
                }
            });

            // Act - make multiple requests to test recovery
            var results = new List<LicenseServerStatus>();
            var exceptions = new List<Exception>();

            for (int i = 0; i < 10; i++)
            {
                try
                {
                    var result = await _queryEngine.QueryLicenseStatusAsync(server, port);
                    results.Add(result);
                }
                catch (Exception ex)
                {
                    exceptions.Add(ex);
                }
            }

            // Assert
            Assert.IsTrue(results.Count > 0, "Should have some successful results after recovery");
            Assert.IsTrue(exceptions.Count < 10, "Should not have all requests fail");

            // Later requests should succeed
            var lastResult = results.Last();
            Assert.IsNotNull(lastResult);
            Assert.IsTrue(lastResult.IsServerUp);

            Console.WriteLine($"Resource exhaustion recovery: {results.Count} successful, {exceptions.Count} failed out of 10 requests");
            Console.WriteLine($"Total process calls: {requestCount}");
        }

        [TestMethod]
        public async Task ConfigurationError_WithInvalidOptions_ShouldValidateAndFail()
        {
            // Arrange - Create engine with invalid configuration
            var invalidEngine = new LicenseQueryEngine(_cacheManager, _processExecutor, _outputParser);
            invalidEngine.Options = new LicenseQueryOptions
            {
                EnableCaching = true,
                CacheExpiration = TimeSpan.Zero, // Invalid - zero expiration
                QueryTimeout = TimeSpan.Zero,     // Invalid - zero timeout
                MaxRetries = -1                  // Invalid - negative retries
            };

            // Act
            var validationErrors = invalidEngine.ValidateConfiguration();

            // Assert
            Assert.IsNotNull(validationErrors);
            Assert.IsTrue(validationErrors.Count > 0);
            Assert.IsTrue(validationErrors.Any(e => e.Contains("expiration") || e.Contains("timeout") || e.Contains("retries")));

            Console.WriteLine($"Configuration validation errors: {validationErrors.Count}");
            foreach (var error in validationErrors)
            {
                Console.WriteLine($"  - {error}");
            }
        }

        [TestMethod]
        public async Task GracefulDegradation_WithMultipleFailureModes_ShouldContinueOperation()
        {
            // Arrange
            var servers = new[] { "timeout-server", "working-server", "error-server", "slow-server" };
            var ports = new[] { 27000, 27001, 27002, 27003 };
            var results = new Dictionary<string, LicenseServerStatus>();
            var errors = new Dictionary<string, Exception>();

            // Set up different failure modes
            _testProcessExecutor.SetMockResult("lmutil.exe", "-c timeout-server@27000", () =>
            {
                throw new TimeoutException("Connection timeout");
            });

            _testProcessExecutor.SetMockResult("lmutil.exe", "-c working-server@27001", new ProcessExecutionResult
            {
                ExitCode = 0,
                Output = _lmstatOutput,
                ExecutionTime = TimeSpan.FromMilliseconds(50),
                Success = true
            });

            _testProcessExecutor.SetMockResult("lmutil.exe", "-c error-server@27002", new ProcessExecutionResult
            {
                ExitCode = 1,
                Error = "Internal server error",
                ExecutionTime = TimeSpan.FromMilliseconds(100),
                Success = false
            });

            _testProcessExecutor.SetMockResult("lmutil.exe", "-c slow-server@27003", new ProcessExecutionResult
            {
                ExitCode = 0,
                Output = _lmstatOutput,
                ExecutionTime = TimeSpan.FromMilliseconds(2000), // Will timeout with default settings
                Success = true
            });

            // Act - attempt to query all servers
            foreach (var (server, port) in servers.Zip(ports, (s, p) => (s, p)))
            {
                try
                {
                    var result = await _queryEngine.QueryLicenseStatusAsync(server, port);
                    results[$"{server}:{port}"] = result;
                }
                catch (Exception ex)
                {
                    errors[$"{server}:{port}"] = ex;
                }
            }

            // Assert
            Assert.AreEqual(1, results.Count, "Should have exactly one working server");
            Assert.AreEqual(3, errors.Count, "Should have errors from 3 failing servers");

            // Verify the working server result
            Assert.IsTrue(results.ContainsKey("working-server:27001"));
            var workingResult = results["working-server:27001"];
            Assert.IsTrue(workingResult.IsServerUp);

            // Verify error types
            Assert.IsTrue(errors.ContainsKey("timeout-server:27000"));
            Assert.IsTrue(errors.ContainsKey("error-server:27002"));
            Assert.IsTrue(errors.ContainsKey("slow-server:27003"));

            Console.WriteLine($"Graceful degradation results:");
            Console.WriteLine($"  Successful servers: {results.Count}");
            Console.WriteLine($"  Failed servers: {errors.Count}");
            Console.WriteLine($"  Working server: {results.Keys.First()}");
            Console.WriteLine($"  Failed servers: {string.Join(", ", errors.Keys)}");
        }

        [TestMethod]
        public async Task RecoveryScenarios_WithIntermittentIssues_ShouldHandleFlakyConnections()
        {
            // Arrange
            var server = "flaky-server";
            var port = 27000;
            var callCount = 0;
            var successes = 0;
            var failures = 0;

            // Simulate flaky connection - works 70% of the time
            _testProcessExecutor.SetMockResult("lmutil.exe", $"-c {server}@{port}", () =>
            {
                callCount++;
                if (new Random().NextDouble() < 0.3) // 30% failure rate
                {
                    return new ProcessExecutionResult
                    {
                        ExitCode = 1,
                        Error = "Connection reset by peer",
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

            // Act - make multiple requests to test overall success rate
            const int totalRequests = 50;
            for (int i = 0; i < totalRequests; i++)
            {
                try
                {
                    await _queryEngine.QueryLicenseStatusAsync(server, port);
                    successes++;
                }
                catch
                {
                    failures++;
                }
            }

            // Assert
            var successRate = successes / (double)totalRequests;

            // With retries, success rate should be much better than the base 70%
            Assert.IsTrue(successRate > 0.85, $"Success rate should be > 85% with retries, got {successRate:P2}");

            Console.WriteLine($"Flaky connection recovery:");
            Console.WriteLine($"  Total requests: {totalRequests}");
            Console.WriteLine($"  Successes: {successes}, Failures: {failures}");
            Console.WriteLine($"  Success rate: {successRate:P2}");
            Console.WriteLine($"  Base reliability: 70%, Achieved reliability: {successRate:P2}");
            Console.WriteLine($"  Total process calls: {callCount}");
        }

        private void SetupDefaultMockResults()
        {
            _testProcessExecutor.SetMockResult("lmutil.exe", "-c license-server@27000", new ProcessExecutionResult
            {
                ExitCode = 0,
                Output = _lmstatOutput,
                ExecutionTime = TimeSpan.FromMilliseconds(50),
                Success = true
            });
        }
    }
}