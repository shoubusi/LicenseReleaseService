using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using LicenseReleaseService.IdleDetection;
using LicenseReleaseService.Configuration;
using LicenseReleaseService.LicenseManagement;
using LicenseReleaseService.LicenseManagement.Caching;
using LicenseReleaseService.LicenseManagement.Models;
using LicenseReleaseService.LicenseManagement.Parsing;
using LicenseReleaseService.Process;

namespace LicenseReleaseService.Tests.Performance
{
    /// <summary>
    /// Performance tests for idle detection components that test load, stress, and performance characteristics
    /// </summary>
    [TestClass]
    public class IdleDetectionPerformanceTests
    {
        private IdleDetectionEngine _idleDetectionEngine;
        private List<IIdleDetector> _detectors;
        private ConcurrentBag<PerformanceMetrics> _metricsBag;
        private Stopwatch _testStopwatch;

        [TestInitialize]
        public void TestInitialize()
        {
            _metricsBag = new ConcurrentBag<PerformanceMetrics>();
            _testStopwatch = new Stopwatch();

            // Create performance-optimized detectors
            _detectors = new List<IIdleDetector>
            {
                new PerformanceTimeBasedDetector(),
                new PerformancePingBasedDetector(),
                new PerformanceActivityMonitor()
            };

            _idleDetectionEngine = new IdleDetectionEngine(
                _detectors,
                new IdleDetectionConfiguration
                {
                    DetectionInterval = 1,
                    Consensus = new ConsensusConfig { MinimumConfidence = 0.7 }
                },
                new Moq.Mock<Microsoft.Extensions.Logging.ILogger<IdleDetectionEngine>>().Object
            );
        }

        [TestCleanup]
        public void TestCleanup()
        {
            _idleDetectionEngine?.Dispose();
            _testStopwatch?.Stop();
        }

        [TestMethod]
        public async Task SingleDetectionPerformance_ShouldMeetPerformanceRequirements()
        {
            // Arrange
            const int warmupRuns = 5;
            const int testRuns = 50;
            var responseTimes = new List<long>();

            var processes = new[]
            {
                new MockProcess(1234, "SLDWORKS.exe", @"C:\SolidWorks 2023\SLDWORKS.exe")
            };

            // Warmup
            for (int i = 0; i < warmupRuns; i++)
            {
                await _idleDetectionEngine.DetectIdleStatesAsync(processes);
            }

            // Act - Performance test
            _testStopwatch.Restart();
            for (int i = 0; i < testRuns; i++)
            {
                var detectionStopwatch = Stopwatch.StartNew();
                await _idleDetectionEngine.DetectIdleStatesAsync(processes);
                detectionStopwatch.Stop();
                responseTimes.Add(detectionStopwatch.ElapsedMilliseconds);
            }
            _testStopwatch.Stop();

            // Assert - Performance validation
            var avgResponseTime = responseTimes.Average();
            var maxResponseTime = responseTimes.Max();
            var minResponseTime = responseTimes.Min();
            var p95ResponseTime = GetPercentile(responseTimes, 95);

            Console.WriteLine($"Single Detection Performance:");
            Console.WriteLine($"  Average: {avgResponseTime:F1}ms");
            Console.WriteLine($"  Min: {minResponseTime:F1}ms");
            Console.WriteLine($"  Max: {maxResponseTime:F1}ms");
            Console.WriteLine($"  95th percentile: {p95ResponseTime:F1}ms");
            Console.WriteLine($"  Total test time: {_testStopwatch.ElapsedMilliseconds}ms");

            // Performance requirements
            Assert.IsTrue(avgResponseTime <= 100, $"Average response time should be <= 100ms, got {avgResponseTime:F1}ms");
            Assert.IsTrue(maxResponseTime <= 500, $"Max response time should be <= 500ms, got {maxResponseTime:F1}ms");
            Assert.IsTrue(p95ResponseTime <= 200, $"95th percentile should be <= 200ms, got {p95ResponseTime:F1}ms");
        }

        [TestMethod]
        public async Task ConcurrentDetectionPerformance_ShouldHandleMultipleProcesses()
        {
            // Arrange
            const int processCount = 25;
            const int concurrentTests = 10;
            var processes = CreateMockProcesses(processCount);
            var executionTimes = new ConcurrentBag<long>();

            // Act - Concurrent detection test
            _testStopwatch.Restart();

            var tasks = new List<Task>();
            for (int i = 0; i < concurrentTests; i++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    var taskStopwatch = Stopwatch.StartNew();
                    await _idleDetectionEngine.DetectIdleStatesAsync(processes);
                    taskStopwatch.Stop();
                    executionTimes.Add(taskStopwatch.ElapsedMilliseconds);
                }));
            }

            await Task.WhenAll(tasks);
            _testStopwatch.Stop();

            // Assert - Concurrent performance validation
            var avgExecutionTime = executionTimes.Average();
            var totalThroughput = (processCount * concurrentTests * 1000.0) / _testStopwatch.ElapsedMilliseconds;

            Console.WriteLine($"Concurrent Detection Performance:");
            Console.WriteLine($"  Processes per test: {processCount}");
            Console.WriteLine($"  Concurrent tests: {concurrentTests}");
            Console.WriteLine($"  Average execution time: {avgExecutionTime:F1}ms");
            Console.WriteLine($"  Total throughput: {totalThroughput:F1} processes/second");
            Console.WriteLine($"  Total test time: {_testStopwatch.ElapsedMilliseconds}ms");

            Assert.IsTrue(avgExecutionTime <= 500, $"Average concurrent execution time should be <= 500ms, got {avgExecutionTime:F1}ms");
            Assert.IsTrue(totalThroughput >= 100, $"Throughput should be >= 100 processes/second, got {totalThroughput:F1}");
        }

        [TestMethod]
        public async Task MemoryUsagePerformance_ShouldStayWithinLimits()
        {
            // Arrange
            const int iterations = 100;
            var processes = CreateMockProcesses(10);
            var initialMemory = GC.GetTotalMemory(true);
            var memoryMeasurements = new List<long>();

            // Act - Memory usage test
            _testStopwatch.Restart();

            for (int i = 0; i < iterations; i++)
            {
                await _idleDetectionEngine.DetectIdleStatesAsync(processes);

                // Force garbage collection and measure memory every 10 iterations
                if (i % 10 == 0)
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();
                    var currentMemory = GC.GetTotalMemory(false);
                    memoryMeasurements.Add(currentMemory);
                }
            }

            _testStopwatch.Stop();

            // Final memory measurement
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            var finalMemory = GC.GetTotalMemory(false);
            var memoryGrowth = finalMemory - initialMemory;

            // Assert - Memory usage validation
            var avgMemoryGrowth = memoryMeasurements.Count > 0 ? memoryMeasurements.Average() - initialMemory : 0;

            Console.WriteLine($"Memory Usage Performance:");
            Console.WriteLine($"  Initial memory: {initialMemory / 1024:F1}KB");
            Console.WriteLine($"  Final memory: {finalMemory / 1024:F1}KB");
            Console.WriteLine($"  Memory growth: {memoryGrowth / 1024:F1}KB");
            Console.WriteLine($"  Average growth: {avgMemoryGrowth / 1024:F1}KB");
            Console.WriteLine($"  Iterations: {iterations}");
            Console.WriteLine($"  Test time: {_testStopwatch.ElapsedMilliseconds}ms");

            Assert.IsTrue(memoryGrowth <= 10 * 1024 * 1024, $"Memory growth should be <= 10MB, got {memoryGrowth / 1024 / 1024:F1}MB");
            Assert.IsTrue(avgMemoryGrowth <= 5 * 1024 * 1024, $"Average memory growth should be <= 5MB, got {avgMemoryGrowth / 1024 / 1024:F1}MB");
        }

        [TestMethod]
        public async Task ScalabilityPerformance_ShouldHandleIncreasingLoad()
        {
            // Arrange
            var processCounts = new[] { 1, 5, 10, 25, 50, 100 };
            var scalabilityResults = new List<ScalabilityResult>();

            // Act - Scalability test
            foreach (var processCount in processCounts)
            {
                var processes = CreateMockProcesses(processCount);
                var warmupRuns = Math.Max(3, 10 / processCount);
                var testRuns = Math.Max(5, 20 / processCount);

                // Warmup
                for (int i = 0; i < warmupRuns; i++)
                {
                    await _idleDetectionEngine.DetectIdleStatesAsync(processes);
                }

                // Performance measurement
                var responseTimes = new List<long>();
                for (int i = 0; i < testRuns; i++)
                {
                    var stopwatch = Stopwatch.StartNew();
                    await _idleDetectionEngine.DetectIdleStatesAsync(processes);
                    stopwatch.Stop();
                    responseTimes.Add(stopwatch.ElapsedMilliseconds);
                }

                var result = new ScalabilityResult
                {
                    ProcessCount = processCount,
                    AverageResponseTime = responseTimes.Average(),
                    MaxResponseTime = responseTimes.Max(),
                    Throughput = (processCount * testRuns * 1000.0) / responseTimes.Sum()
                };

                scalabilityResults.Add(result);
            }

            // Assert - Scalability validation
            Console.WriteLine($"Scalability Performance:");
            foreach (var result in scalabilityResults)
            {
                Console.WriteLine($"  {result.ProcessCount,3} processes: {result.AverageResponseTime,6:F1}ms avg, {result.MaxResponseTime,6:F1}ms max, {result.Throughput,6:F1} proc/s");
            }

            // Verify linear scalability (response time should not grow exponentially)
            var smallProcessResult = scalabilityResults.First(r => r.ProcessCount <= 5);
            var largeProcessResult = scalabilityResults.Last(r => r.ProcessCount >= 50);

            var expectedTimeRatio = (double)largeProcessResult.ProcessCount / smallProcessResult.ProcessCount;
            var actualTimeRatio = largeProcessResult.AverageResponseTime / smallProcessResult.AverageResponseTime;

            Console.WriteLine($"Expected time ratio (linear): {expectedTimeRatio:F2}");
            Console.WriteLine($"Actual time ratio: {actualTimeRatio:F2}");

            Assert.IsTrue(actualTimeRatio <= expectedTimeRatio * 2,
                $"Response time should scale linearly. Expected ratio <= {expectedTimeRatio * 2:F2}, got {actualTimeRatio:F2}");
        }

        [TestMethod]
        public async Task StressTest_ShouldHandleSustainedHighLoad()
        {
            // Arrange
            const int durationSeconds = 30;
            const int processCount = 20;
            var processes = CreateMockProcesses(processCount);
            var cancellationTokenSource = new CancellationTokenSource();
            var completedDetections = 0;
            var errorCount = 0;
            var responseTimes = new ConcurrentBag<long>();

            // Act - Stress test
            _testStopwatch.Restart();

            var detectionTask = Task.Run(async () =>
            {
                while (!cancellationTokenSource.Token.IsCancellationRequested)
                {
                    try
                    {
                        var stopwatch = Stopwatch.StartNew();
                        await _idleDetectionEngine.DetectIdleStatesAsync(processes);
                        stopwatch.Stop();

                        responseTimes.Add(stopwatch.ElapsedMilliseconds);
                        Interlocked.Increment(ref completedDetections);

                        // Small delay to simulate realistic load
                        await Task.Delay(100, cancellationTokenSource.Token);
                    }
                    catch (Exception ex)
                    {
                        Interlocked.Increment(ref errorCount);
                        Console.WriteLine($"Detection error: {ex.Message}");
                    }
                }
            });

            // Run for specified duration
            await Task.Delay(TimeSpan.FromSeconds(durationSeconds));
            cancellationTokenSource.Cancel();

            try
            {
                await detectionTask;
            }
            catch (OperationCanceledException)
            {
                // Expected
            }

            _testStopwatch.Stop();

            // Assert - Stress test validation
            var avgResponseTime = responseTimes.Any() ? responseTimes.Average() : 0;
            var errorRate = completedDetections > 0 ? (double)errorCount / completedDetections : 0;
            var throughput = completedDetections / (durationSeconds / 60.0); // detections per minute

            Console.WriteLine($"Stress Test Results:");
            Console.WriteLine($"  Duration: {durationSeconds} seconds");
            Console.WriteLine($"  Completed detections: {completedDetections}");
            Console.WriteLine($"  Errors: {errorCount}");
            Console.WriteLine($"  Error rate: {errorRate:P4}");
            Console.WriteLine($"  Average response time: {avgResponseTime:F1}ms");
            Console.WriteLine($"  Throughput: {throughput:F1} detections/minute");
            Console.WriteLine($"  Processes per detection: {processCount}");

            Assert.IsTrue(errorRate <= 0.01, $"Error rate should be <= 1%, got {errorRate:P4}");
            Assert.IsTrue(throughput >= 60, $"Throughput should be >= 60 detections/minute, got {throughput:F1}");
            Assert.IsTrue(avgResponseTime <= 1000, $"Average response time should be <= 1000ms under stress, got {avgResponseTime:F1}ms");
        }

        [TestMethod]
        public async Task DetectorPerformanceComparison_ShouldBenchmarkDifferentDetectors()
        {
            // Arrange
            const int testRuns = 20;
            var processes = CreateMockProcesses(5);
            var detectorMetrics = new Dictionary<string, List<long>>();

            // Test each detector individually
            foreach (var detector in _detectors)
            {
                var engine = new IdleDetectionEngine(
                    new List<IIdleDetector> { detector },
                    new IdleDetectionConfiguration { DetectionInterval = 1 },
                    new Moq.Mock<Microsoft.Extensions.Logging.ILogger<IdleDetectionEngine>>().Object
                );

                var responseTimes = new List<long>();

                // Warmup
                for (int i = 0; i < 3; i++)
                {
                    await engine.DetectIdleStatesAsync(processes);
                }

                // Performance test
                for (int i = 0; i < testRuns; i++)
                {
                    var stopwatch = Stopwatch.StartNew();
                    await engine.DetectIdleStatesAsync(processes);
                    stopwatch.Stop();
                    responseTimes.Add(stopwatch.ElapsedMilliseconds);
                }

                detectorMetrics[detector.GetType().Name] = responseTimes;
            }

            // Assert - Performance comparison
            Console.WriteLine($"Detector Performance Comparison:");
            foreach (var metricPair in detectorMetrics)
            {
                var detectorName = metricPair.Key;
                var responseTimes = metricPair.Value;
                var avgTime = responseTimes.Average();
                var maxTime = responseTimes.Max();

                Console.WriteLine($"  {detectorName}:");
                Console.WriteLine($"    Average: {avgTime:F1}ms");
                Console.WriteLine($"    Max: {maxTime:F1}ms");
            }

            // All detectors should meet performance requirements
            foreach (var metricPair in detectorMetrics)
            {
                var avgTime = metricPair.Value.Average();
                Assert.IsTrue(avgTime <= 200, $"{metricPair.Key} average should be <= 200ms, got {avgTime:F1}ms");
            }
        }

        [TestMethod]
        public async Task ConfigurationImpactPerformance_ShouldMeasureConfigChanges()
        {
            // Arrange
            var processes = CreateMockProcesses(10);
            const int testRuns = 15;
            var configurations = new[]
            {
                new IdleDetectionConfiguration
                {
                    DetectionInterval = 1,
                    Consensus = new ConsensusConfig { MinimumConfidence = 0.5 }
                },
                new IdleDetectionConfiguration
                {
                    DetectionInterval = 1,
                    Consensus = new ConsensusConfig { MinimumConfidence = 0.7 }
                },
                new IdleDetectionConfiguration
                {
                    DetectionInterval = 1,
                    Consensus = new ConsensusConfig { MinimumConfidence = 0.9, RequireAllDetectors = true }
                }
            };

            var configResults = new List<ConfigurationPerformanceResult>();

            // Act - Test different configurations
            foreach (var config in configurations)
            {
                var engine = new IdleDetectionEngine(
                    _detectors,
                    config,
                    new Moq.Mock<Microsoft.Extensions.Logging.ILogger<IdleDetectionEngine>>().Object
                );

                var responseTimes = new List<long>();

                // Warmup
                for (int i = 0; i < 3; i++)
                {
                    await engine.DetectIdleStatesAsync(processes);
                }

                // Performance test
                for (int i = 0; i < testRuns; i++)
                {
                    var stopwatch = Stopwatch.StartNew();
                    await engine.DetectIdleStatesAsync(processes);
                    stopwatch.Stop();
                    responseTimes.Add(stopwatch.ElapsedMilliseconds);
                }

                var result = new ConfigurationPerformanceResult
                {
                    Configuration = config,
                    AverageResponseTime = responseTimes.Average(),
                    MaxResponseTime = responseTimes.Max(),
                    ConfidenceThreshold = config.Consensus.MinimumConfidence,
                    RequireAllDetectors = config.Consensus.RequireAllDetectors
                };

                configResults.Add(result);
            }

            // Assert - Configuration impact validation
            Console.WriteLine($"Configuration Impact Performance:");
            foreach (var result in configResults)
            {
                Console.WriteLine($"  Confidence: {result.ConfidenceThreshold}, RequireAll: {result.RequireAllDetectors}");
                Console.WriteLine($"    Average: {result.AverageResponseTime:F1}ms, Max: {result.MaxResponseTime:F1}ms");
            }

            // Stricter configurations should not significantly impact performance
            var lenientConfig = configResults.First(r => r.ConfidenceThreshold == 0.5);
            var strictConfig = configResults.Last(r => r.ConfidenceThreshold == 0.9);

            var performanceOverhead = strictConfig.AverageResponseTime / lenientConfig.AverageResponseTime;
            Console.WriteLine($"Performance overhead for strict config: {performanceOverhead:P2}");

            Assert.IsTrue(performanceOverhead <= 1.5,
                $"Strict configuration should not add more than 50% overhead, got {performanceOverhead:P2} overhead");
        }

        [TestMethod]
        public async Task ResourceCleanupPerformance_ShouldMeasureDisposalTime()
        {
            // Arrange
            const int testIterations = 10;
            var disposalTimes = new List<long>();
            var processes = CreateMockProcesses(5);

            // Act - Resource cleanup performance test
            for (int i = 0; i < testIterations; i++)
            {
                var engine = new IdleDetectionEngine(
                    _detectors,
                    new IdleDetectionConfiguration { DetectionInterval = 1 },
                    new Moq.Mock<Microsoft.Extensions.Logging.ILogger<IdleDetectionEngine>>().Object
                );

                // Use the engine
                await engine.DetectIdleStatesAsync(processes);

                // Measure disposal time
                var stopwatch = Stopwatch.StartNew();
                engine.Dispose();
                stopwatch.Stop();

                disposalTimes.Add(stopwatch.ElapsedMilliseconds);
            }

            // Assert - Cleanup performance validation
            var avgDisposalTime = disposalTimes.Average();
            var maxDisposalTime = disposalTimes.Max();

            Console.WriteLine($"Resource Cleanup Performance:");
            Console.WriteLine($"  Average disposal time: {avgDisposalTime:F1}ms");
            Console.WriteLine($"  Max disposal time: {maxDisposalTime:F1}ms");
            Console.WriteLine($"  Test iterations: {testIterations}");

            Assert.IsTrue(avgDisposalTime <= 100, $"Average disposal time should be <= 100ms, got {avgDisposalTime:F1}ms");
            Assert.IsTrue(maxDisposalTime <= 500, $"Max disposal time should be <= 500ms, got {maxDisposalTime:F1}ms");
        }

        #region Helper Methods

        private MockProcess[] CreateMockProcesses(int count)
        {
            var processes = new List<MockProcess>();
            for (int i = 0; i < count; i++)
            {
                var version = (i % 2) + 1;
                var path = version == 1 ? @"C:\SolidWorks 2023\SLDWORKS.exe" : @"C:\SolidWorks 2024\SLDWORKS.exe";

                processes.Add(new MockProcess(1000 + i, "SLDWORKS.exe", path)
                {
                    LastActivityTime = DateTime.UtcNow.AddMinutes(-10 - (i % 20)),
                    IsResponding = i % 3 != 0
                });
            }
            return processes.ToArray();
        }

        private double GetPercentile(List<long> values, double percentile)
        {
            if (values.Count == 0) return 0;

            var sortedValues = values.OrderBy(v => v).ToList();
            var index = (int)Math.Ceiling(percentile / 100.0 * sortedValues.Count) - 1;
            index = Math.Max(0, Math.Min(index, sortedValues.Count - 1));

            return sortedValues[index];
        }

        #endregion

        #region Helper Classes

        internal class PerformanceMetrics
        {
            public long ExecutionTimeMs { get; set; }
            public int ProcessCount { get; set; }
            public DateTime Timestamp { get; set; }
            public string TestName { get; set; }
        }

        internal class ScalabilityResult
        {
            public int ProcessCount { get; set; }
            public double AverageResponseTime { get; set; }
            public double MaxResponseTime { get; set; }
            public double Throughput { get; set; }
        }

        internal class ConfigurationPerformanceResult
        {
            public IdleDetectionConfiguration Configuration { get; set; }
            public double AverageResponseTime { get; set; }
            public double MaxResponseTime { get; set; }
            public double ConfidenceThreshold { get; set; }
            public bool RequireAllDetectors { get; set; }
        }

        internal class MockProcess : Process
        {
            private readonly int _id;
            private readonly string _processName;
            private readonly string _mainModulePath;
            public DateTime LastActivityTime { get; set; }
            public bool IsResponding { get; set; }

            public MockProcess(int id, string processName, string mainModulePath)
            {
                _id = id;
                _processName = processName;
                _mainModulePath = mainModulePath;
                LastActivityTime = DateTime.UtcNow.AddMinutes(-10);
                IsResponding = true;
            }

            public new int Id => _id;
            public new string ProcessName => _processName;
            public new DateTime StartTime => DateTime.UtcNow.AddHours(-2);
            public new bool Responding => IsResponding;

            public new ProcessModule MainModule => new MockProcessModule(_mainModulePath);
        }

        internal class MockProcessModule : ProcessModule
        {
            private readonly string _fileName;

            public MockProcessModule(string fileName)
            {
                _fileName = fileName;
            }

            public new string FileName => _fileName;
        }

        internal class PerformanceTimeBasedDetector : IIdleDetector
        {
            public async Task<DetectorResult> DetectIdleAsync(Process process)
            {
                await Task.Delay(5); // Fast processing
                var mockProcess = process as MockProcess;
                var idleDuration = DateTime.UtcNow - mockProcess.LastActivityTime;

                return new DetectorResult
                {
                    DetectorType = "TimeBased",
                    IsIdle = idleDuration.TotalMinutes > 15,
                    Confidence = Math.Min(idleDuration.TotalMinutes / 30.0, 1.0),
                    LastActivityTime = mockProcess.LastActivityTime,
                    IdleDuration = idleDuration
                };
            }
        }

        internal class PerformancePingBasedDetector : IIdleDetector
        {
            public async Task<DetectorResult> DetectIdleAsync(Process process)
            {
                await Task.Delay(8); // Medium processing time
                var mockProcess = process as MockProcess;

                return new DetectorResult
                {
                    DetectorType = "PingBased",
                    IsIdle = !mockProcess.IsResponding,
                    Confidence = mockProcess.IsResponding ? 0.1 : 0.9,
                    LastActivityTime = mockProcess.LastActivityTime,
                    IdleDuration = DateTime.UtcNow - mockProcess.LastActivityTime
                };
            }
        }

        internal class PerformanceActivityMonitor : IIdleDetector
        {
            public async Task<DetectorResult> DetectIdleAsync(Process process)
            {
                await Task.Delay(3); // Fastest processing time
                var mockProcess = process as MockProcess;

                return new DetectorResult
                {
                    DetectorType = "ActivityMonitor",
                    IsIdle = (DateTime.UtcNow - mockProcess.LastActivityTime).TotalMinutes > 5,
                    Confidence = 0.8,
                    LastActivityTime = mockProcess.LastActivityTime,
                    IdleDuration = DateTime.UtcNow - mockProcess.LastActivityTime
                };
            }
        }

        #endregion
    }
}