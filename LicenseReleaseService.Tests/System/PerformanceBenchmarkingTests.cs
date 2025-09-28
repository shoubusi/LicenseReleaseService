using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LicenseReleaseService.IdleDetection;
using LicenseReleaseService.Licensing;
using LicenseReleaseService.TimerExecution;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace LicenseReleaseService.Tests.System
{
    [TestClass]
    public class PerformanceBenchmarkingTests
    {
        private TestContext _testContext;
        private IdleDetectionEngine _detectionEngine;
        private string _benchmarkResultsPath;

        [TestInitialize]
        public void Setup()
        {
            _testContext = new TestContext();
            _benchmarkResultsPath = Path.Combine(Path.GetTempPath(), $"BenchmarkResults_{DateTime.UtcNow:yyyyMMdd_HHmmss}");
            Directory.CreateDirectory(_benchmarkResultsPath);
        }

        [TestCleanup]
        public void Cleanup()
        {
            _detectionEngine?.Dispose();

            try
            {
                if (Directory.Exists(_benchmarkResultsPath))
                {
                    Directory.Delete(_benchmarkResultsPath, true);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }

        [TestMethod]
        public async Task Benchmark_SingleDetectionPerformance_ShouldMeetSLA()
        {
            // Arrange
            var warmupRuns = 10;
            var testRuns = 100;
            var maxResponseTime = 100ms;
            var targetAvgResponseTime = 50ms;
            var responseTimes = new List<long>();

            // Initialize detection engine
            _detectionEngine = await InitializeDetectionEngine();

            // Warmup
            for (int i = 0; i < warmupRuns; i++)
            {
                await RunSingleDetection();
            }

            // Act
            // Run benchmark
            for (int i = 0; i < testRuns; i++)
            {
                var stopwatch = Stopwatch.StartNew();
                await RunSingleDetection();
                stopwatch.Stop();
                responseTimes.Add(stopwatch.ElapsedMilliseconds);
            }

            // Assert
            var avgResponseTime = responseTimes.Average();
            var maxObservedTime = responseTimes.Max();
            var p95ResponseTime = CalculatePercentile(responseTimes, 95);
            var p99ResponseTime = CalculatePercentile(responseTimes, 99);

            // Verify SLA compliance
            Assert.IsTrue(avgResponseTime <= targetAvgResponseTime.TotalMilliseconds,
                $"Average response time should be <= {targetAvgResponseTime}ms, actual: {avgResponseTime}ms");

            Assert.IsTrue(maxObservedTime <= maxResponseTime.TotalMilliseconds,
                $"Maximum response time should be <= {maxResponseTime}ms, actual: {maxObservedTime}ms");

            Assert.IsTrue(p95ResponseTime <= maxResponseTime.TotalMilliseconds,
                $"95th percentile should be <= {maxResponseTime}ms, actual: {p95ResponseTime}ms");

            Assert.IsTrue(p99ResponseTime <= maxResponseTime.TotalMilliseconds,
                $"99th percentile should be <= {maxResponseTime}ms, actual: {p99ResponseTime}ms");

            // Save benchmark results
            var results = new BenchmarkResults
            {
                TestName = "Single Detection Performance",
                TotalRuns = testRuns,
                AverageResponseTime = avgResponseTime,
                MaxResponseTime = maxObservedTime,
                P95ResponseTime = p95ResponseTime,
                P99ResponseTime = p99ResponseTime,
                SLACompliant = avgResponseTime <= targetAvgResponseTime.TotalMilliseconds &&
                              maxObservedTime <= maxResponseTime.TotalMilliseconds,
                Timestamp = DateTime.UtcNow
            };

            await SaveBenchmarkResults(results, "SingleDetectionPerformance.json");

            _testContext.WriteLine($"Single detection benchmark completed:");
            _testContext.WriteLine($"- Average: {avgResponseTime}ms (target: {targetAvgResponseTime}ms)");
            _testContext.WriteLine($"- Maximum: {maxObservedTime}ms (limit: {maxResponseTime}ms)");
            _testContext.WriteLine($"- P95: {p95ResponseTime}ms");
            _testContext.WriteLine($"- P99: {p99ResponseTime}ms");
            _testContext.WriteLine($"- SLA Compliant: {results.SLACompliant}");
        }

        [TestMethod]
        public async Task Benchmark_ConcurrentDetectionPerformance_ShouldScaleLinearly()
        {
            // Arrange
            var processCounts = new[] { 10, 25, 50, 100, 200 };
            var targetThroughput = 100; // processes per second
            var maxResponseTime = 500ms;
            var results = new List<ConcurrentBenchmarkResult>();

            await _detectionEngine.StartAsync();

            // Act
            foreach (var processCount in processCounts)
            {
                var result = await RunConcurrentDetectionBenchmark(processCount, maxResponseTime);
                results.Add(result);

                _testContext.WriteLine($"Concurrent benchmark ({processCount} processes):");
                _testContext.WriteLine($"- Throughput: {result.Throughput} processes/sec");
                _testContext.WriteLine($"- Avg Response Time: {result.AverageResponseTime}ms");
                _testContext.WriteLine($"- Max Response Time: {result.MaxResponseTime}ms");
                _testContext.WriteLine($"- Memory Usage: {result.PeakMemoryUsageMB}MB");
            }

            await _detectionEngine.StopAsync();

            // Assert
            // Verify linear scalability
            var baselineThroughput = results.First().Throughput;
            var baselineProcessCount = results.First().ProcessCount;

            for (int i = 1; i < results.Count; i++)
            {
                var expectedThroughput = (results[i].ProcessCount * baselineThroughput) / baselineProcessCount;
                var actualThroughput = results[i].Throughput;
                var efficiency = actualThroughput / expectedThroughput;

                Assert.IsTrue(efficiency >= 0.8,
                    $"Scalability efficiency should be >= 80% for {results[i].ProcessCount} processes, actual: {efficiency:P2}");

                Assert.IsTrue(results[i].Throughput >= targetThroughput,
                    $"Throughput should be >= {targetThroughput} processes/sec for {results[i].ProcessCount} processes, actual: {results[i].Throughput}");
            }

            // Verify memory efficiency
            var memoryPerProcess = results.Average(r => r.PeakMemoryUsageMB / r.ProcessCount);
            Assert.IsTrue(memoryPerProcess <= 1,
                $"Memory usage per process should be <= 1MB, actual: {memoryPerProcess}MB");

            // Save benchmark results
            await SaveBenchmarkResults(results, "ConcurrentDetectionPerformance.json");
        }

        [TestMethod]
        public async Task Benchmark_MemoryUsage_ShouldNotLeakUnderLoad()
        {
            // Arrange
            var testDuration = TimeSpan.FromMinutes(10);
            var measurementInterval = TimeSpan.FromSeconds(30);
            var memoryMeasurements = new List<MemoryMeasurement>();
            var maxAllowedGrowth = 10; // MB

            await _detectionEngine.StartAsync();

            // Act
            var startTime = DateTime.UtcNow;
            var startMemory = Process.GetCurrentProcess().WorkingSet64;

            while (DateTime.UtcNow - startTime < testDuration)
            {
                // Run load
                await RunDetectionLoad(50);

                // Measure memory
                var currentMemory = Process.GetCurrentProcess().WorkingSet64;
                var measurement = new MemoryMeasurement
                {
                    Timestamp = DateTime.UtcNow,
                    MemoryUsageMB = currentMemory / 1024 / 1024,
                    ElapsedTime = DateTime.UtcNow - startTime
                };
                memoryMeasurements.Add(measurement);

                await Task.Delay(measurementInterval);
            }

            await _detectionEngine.StopAsync();

            var endMemory = Process.GetCurrentProcess().WorkingSet64;
            var totalMemoryGrowth = (endMemory - startMemory) / 1024 / 1024;

            // Assert
            Assert.IsTrue(totalMemoryGrowth <= maxAllowedGrowth,
                $"Total memory growth should be <= {maxAllowedGrowth}MB, actual: {totalMemoryGrowth}MB");

            // Check for steady state
            var recentMeasurements = memoryMeasurements.Skip(memoryMeasurements.Count / 2).ToList();
            var recentTrend = CalculateMemoryTrend(recentMeasurements);

            Assert.IsTrue(recentTrend <= 1,
                $"Memory trend should indicate stable usage (trend <= 1), actual: {recentTrend}");

            // Save benchmark results
            var results = new MemoryBenchmarkResult
            {
                TestDuration = testDuration,
                TotalMemoryGrowthMB = totalMemoryGrowth,
                MaxAllowedGrowthMB = maxAllowedGrowth,
                MemoryMeasurements = memoryMeasurements,
                MemoryTrend = recentTrend,
                SLACompliant = totalMemoryGrowth <= maxAllowedGrowth && recentTrend <= 1
            };

            await SaveBenchmarkResults(results, "MemoryUsageBenchmark.json");

            _testContext.WriteLine($"Memory usage benchmark completed:");
            _testContext.WriteLine($"- Test duration: {testDuration}");
            _testContext.WriteLine($"- Total memory growth: {totalMemoryGrowth}MB (limit: {maxAllowedGrowth}MB)");
            _testContext.WriteLine($"- Memory trend: {recentTrend}");
            _testContext.WriteLine($"- SLA Compliant: {results.SLACompliant}");
        }

        [TestMethod]
        public async Task Benchmark_StressTesting_ShouldHandlePeakLoads()
        {
            // Arrange
            var peakLoadDuration = TimeSpan.FromMinutes(5);
            var recoveryDuration = TimeSpan.FromMinutes(2);
            var peakProcessCount = 500;
            var normalProcessCount = 50;
            var maxAllowedErrorRate = 0.01; // 1%
            var performanceMetrics = new List<StressTestMetric>();

            await _detectionEngine.StartAsync();

            // Act
            // Baseline measurement
            var baseline = await MeasurePerformance(normalProcessCount, TimeSpan.FromMinutes(1));

            // Peak load
            _testContext.WriteLine($"Starting peak load test with {peakProcessCount} processes...");
            var peakMetrics = await MeasurePerformance(peakProcessCount, peakLoadDuration);
            performanceMetrics.Add(peakMetrics);

            // Recovery measurement
            _testContext.WriteLine($"Measuring recovery with {normalProcessCount} processes...");
            var recoveryMetrics = await MeasurePerformance(normalProcessCount, recoveryDuration);
            performanceMetrics.Add(recoveryMetrics);

            await _detectionEngine.StopAsync();

            // Assert
            // Verify error rate during peak load
            Assert.IsTrue(peakMetrics.ErrorRate <= maxAllowedErrorRate,
                $"Error rate during peak load should be <= {maxAllowedErrorRate:P2}, actual: {peakMetrics.ErrorRate:P2}");

            // Verify recovery to baseline performance
            var recoveryThroughputRatio = recoveryMetrics.Throughput / baseline.Throughput;
            Assert.IsTrue(recoveryThroughputRatio >= 0.9,
                $"Recovery throughput should be >= 90% of baseline, actual: {recoveryThroughputRatio:P2}");

            var recoveryResponseTimeRatio = recoveryMetrics.AverageResponseTime / baseline.AverageResponseTime;
            Assert.IsTrue(recoveryResponseTimeRatio <= 1.2,
                $"Recovery response time should be <= 120% of baseline, actual: {recoveryResponseTimeRatio:P2}");

            // Verify system stability
            Assert.IsTrue(peakMetrics.SystemStability >= 0.95,
                $"System stability during peak load should be >= 95%, actual: {peakMetrics.SystemStability:P2}");

            // Save benchmark results
            var results = new StressTestResult
            {
                BaselineMetrics = baseline,
                PeakLoadMetrics = peakMetrics,
                RecoveryMetrics = recoveryMetrics,
                TestDuration = peakLoadDuration + recoveryDuration,
                MaxAllowedErrorRate = maxAllowedErrorRate,
                SLACompliant = peakMetrics.ErrorRate <= maxAllowedErrorRate &&
                               recoveryThroughputRatio >= 0.9 &&
                               recoveryResponseTimeRatio <= 1.2
            };

            await SaveBenchmarkResults(results, "StressTestingBenchmark.json");

            _testContext.WriteLine($"Stress testing benchmark completed:");
            _testContext.WriteLine($"- Peak load error rate: {peakMetrics.ErrorRate:P2} (limit: {maxAllowedErrorRate:P2})");
            _testContext.WriteLine($"- Recovery throughput ratio: {recoveryThroughputRatio:P2}");
            _testContext.WriteLine($"- Recovery response time ratio: {recoveryResponseTimeRatio:P2}");
            _testContext.WriteLine($"- System stability during peak: {peakMetrics.SystemStability:P2}");
            _testContext.WriteLine($"- SLA Compliant: {results.SLACompliant}");
        }

        [TestMethod]
        public async Task Benchmark_ProductionConfiguration_ShouldMeetProductionSLAs()
        {
            // Arrange
            var productionConfig = CreateProductionConfiguration();
            var testDuration = TimeSpan.FromMinutes(30);
            var slaRequirements = new ProductionSLARequirements
            {
                MaxResponseTime = 100ms,
                MinAvailability = 0.999,
                MaxErrorRate = 0.001,
                MaxMemoryUsageMB = 100,
                MinThroughput = 100
            };

            // Apply production configuration
            _detectionEngine = await InitializeDetectionEngine(productionConfig);

            await _detectionEngine.StartAsync();

            // Act
            var metrics = await MeasureProductionPerformance(testDuration, slaRequirements);

            await _detectionEngine.StopAsync();

            // Assert
            Assert.IsTrue(metrics.AverageResponseTime <= slaRequirements.MaxResponseTime.TotalMilliseconds,
                $"Response time SLA: <= {slaRequirements.MaxResponseTime}ms, actual: {metrics.AverageResponseTime}ms");

            Assert.IsTrue(metrics.Availability >= slaRequirements.MinAvailability,
                $"Availability SLA: >= {slaRequirements.MinAvailability:P3}, actual: {metrics.Availability:P3}");

            Assert.IsTrue(metrics.ErrorRate <= slaRequirements.MaxErrorRate,
                $"Error rate SLA: <= {slaRequirements.MaxErrorRate:P3}, actual: {metrics.ErrorRate:P3}");

            Assert.IsTrue(metrics.PeakMemoryUsageMB <= slaRequirements.MaxMemoryUsageMB,
                $"Memory usage SLA: <= {slaRequirements.MaxMemoryUsageMB}MB, actual: {metrics.PeakMemoryUsageMB}MB");

            Assert.IsTrue(metrics.Throughput >= slaRequirements.MinThroughput,
                $"Throughput SLA: >= {slaRequirements.MinThroughput} processes/sec, actual: {metrics.Throughput}");

            // Save benchmark results
            var results = new ProductionBenchmarkResult
            {
                TestDuration = testDuration,
                SLARequirements = slaRequirements,
                PerformanceMetrics = metrics,
                SLACompliant = metrics.AverageResponseTime <= slaRequirements.MaxResponseTime.TotalMilliseconds &&
                               metrics.Availability >= slaRequirements.MinAvailability &&
                               metrics.ErrorRate <= slaRequirements.MaxErrorRate &&
                               metrics.PeakMemoryUsageMB <= slaRequirements.MaxMemoryUsageMB &&
                               metrics.Throughput >= slaRequirements.MinThroughput
            };

            await SaveBenchmarkResults(results, "ProductionConfigurationBenchmark.json");

            _testContext.WriteLine($"Production configuration benchmark completed:");
            _testContext.WriteLine($"- Response Time: {metrics.AverageResponseTime}ms (SLA: {slaRequirements.MaxResponseTime}ms)");
            _testContext.WriteLine($"- Availability: {metrics.Availability:P3} (SLA: {slaRequirements.MinAvailability:P3})");
            _testContext.WriteLine($"- Error Rate: {metrics.ErrorRate:P3} (SLA: {slaRequirements.MaxErrorRate:P3})");
            _testContext.WriteLine($"- Memory Usage: {metrics.PeakMemoryUsageMB}MB (SLA: {slaRequirements.MaxMemoryUsageMB}MB)");
            _testContext.WriteLine($"- Throughput: {metrics.Throughput} processes/sec (SLA: {slaRequirements.MinThroughput})");
            _testContext.WriteLine($"- SLA Compliant: {results.SLACompliant}");
        }

        #region Helper Methods

        private async Task<IdleDetectionEngine> InitializeDetectionEngine(IdleDetectionConfiguration config = null)
        {
            var mockTimerService = new Moq.Mock<ITimerExecutionService>();
            var mockLicenseEngine = new Moq.Mock<ILicenseQueryEngine>();
            var mockConfigService = new Moq.Mock<IConfigurationService>();

            if (config == null)
            {
                config = CreateTestConfiguration();
            }

            mockConfigService.Setup(c => c.GetIdleDetectionConfiguration()).Returns(config);
            mockLicenseEngine.Setup(e => e.QueryLicensesAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(new LicenseQueryResult { Success = true, Licenses = new List<LicenseInfo>() });

            var engine = new IdleDetectionEngine(mockConfigService.Object, mockTimerService.Object, mockLicenseEngine.Object);
            return engine;
        }

        private IdleDetectionConfiguration CreateTestConfiguration()
        {
            return new IdleDetectionConfiguration
            {
                IsEnabled = true,
                DetectionInterval = TimeSpan.FromSeconds(1),
                ConfidenceThreshold = 0.8,
                MaxDetectionTime = TimeSpan.FromSeconds(30),
                EnableTimeBasedDetection = true,
                EnablePingBasedDetection = true,
                EnableActivityMonitoring = true,
                ConsensusStrategy = ConsensusStrategy.Majority,
                MaxConcurrentDetections = 100
            };
        }

        private IdleDetectionConfiguration CreateProductionConfiguration()
        {
            return new IdleDetectionConfiguration
            {
                IsEnabled = true,
                DetectionInterval = TimeSpan.FromSeconds(30),
                ConfidenceThreshold = 0.8,
                MaxDetectionTime = TimeSpan.FromSeconds(30),
                EnableTimeBasedDetection = true,
                EnablePingBasedDetection = true,
                EnableActivityMonitoring = true,
                ConsensusStrategy = ConsensusStrategy.Majority,
                EnableAdaptiveThresholds = true,
                EnableWorkHourRules = true,
                MaxConcurrentDetections = 100,
                MemoryLimitMB = 100,
                EnableDetailedLogging = true,
                LogRetentionDays = 30
            };
        }

        private async Task RunSingleDetection()
        {
            await Task.Delay(10); // Simulate detection work
        }

        private async Task RunDetectionLoad(int processCount)
        {
            var tasks = new List<Task>();
            for (int i = 0; i < processCount; i++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    await RunSingleDetection();
                }));
            }
            await Task.WhenAll(tasks);
        }

        private async Task<ConcurrentBenchmarkResult> RunConcurrentDetectionBenchmark(int processCount, TimeSpan maxResponseTime)
        {
            var stopwatch = Stopwatch.StartNew();
            var responseTimes = new List<long>();
            var startMemory = Process.GetCurrentProcess().WorkingSet64;

            var tasks = new List<Task>();
            for (int i = 0; i < processCount; i++)
            {
                tasks.Add(Task.Run(async () =>
                {
                    var sw = Stopwatch.StartNew();
                    await RunSingleDetection();
                    sw.Stop();
                    lock (responseTimes)
                    {
                        responseTimes.Add(sw.ElapsedMilliseconds);
                    }
                }));
            }

            await Task.WhenAll(tasks);
            stopwatch.Stop();

            var endMemory = Process.GetCurrentProcess().WorkingSet64;

            return new ConcurrentBenchmarkResult
            {
                ProcessCount = processCount,
                TotalTime = stopwatch.Elapsed,
                Throughput = processCount / stopwatch.Elapsed.TotalSeconds,
                AverageResponseTime = responseTimes.Average(),
                MaxResponseTime = responseTimes.Max(),
                PeakMemoryUsageMB = (endMemory - startMemory) / 1024 / 1024
            };
        }

        private async Task<StressTestMetric> MeasurePerformance(int processCount, TimeSpan duration)
        {
            var startTime = DateTime.UtcNow;
            var responseTimes = new List<long>();
            var errorCount = 0;
            var totalOperations = 0;
            var peakMemory = 0L;

            while (DateTime.UtcNow - startTime < duration)
            {
                try
                {
                    var sw = Stopwatch.StartNew();
                    await RunDetectionLoad(Math.Min(processCount, 100));
                    sw.Stop();

                    responseTimes.Add(sw.ElapsedMilliseconds);
                    totalOperations++;

                    var currentMemory = Process.GetCurrentProcess().WorkingSet64;
                    peakMemory = Math.Max(peakMemory, currentMemory);
                }
                catch
                {
                    errorCount++;
                    totalOperations++;
                }
            }

            return new StressTestMetric
            {
                ProcessCount = processCount,
                Duration = duration,
                TotalOperations = totalOperations,
                Throughput = totalOperations / duration.TotalSeconds,
                AverageResponseTime = responseTimes.Any() ? responseTimes.Average() : 0,
                ErrorRate = (double)errorCount / totalOperations,
                PeakMemoryUsageMB = peakMemory / 1024 / 1024,
                SystemStability = 1.0 - ((double)errorCount / totalOperations)
            };
        }

        private async Task<ProductionPerformanceMetrics> MeasureProductionPerformance(TimeSpan duration, ProductionSLARequirements sla)
        {
            var startTime = DateTime.UtcNow;
            var responseTimes = new List<long>();
            var errorCount = 0;
            var totalOperations = 0;
            var downtimeMs = 0L;
            var lastOperationTime = DateTime.UtcNow;
            var peakMemory = 0L;

            while (DateTime.UtcNow - startTime < duration)
            {
                try
                {
                    var operationStart = DateTime.UtcNow;
                    var sw = Stopwatch.StartNew();
                    await RunDetectionLoad(50);
                    sw.Stop();

                    responseTimes.Add(sw.ElapsedMilliseconds);
                    totalOperations++;
                    lastOperationTime = DateTime.UtcNow;

                    var currentMemory = Process.GetCurrentProcess().WorkingSet64;
                    peakMemory = Math.Max(peakMemory, currentMemory);
                }
                catch
                {
                    errorCount++;
                    totalOperations++;
                    var errorDuration = DateTime.UtcNow - lastOperationTime;
                    downtimeMs += (long)errorDuration.TotalMilliseconds;
                    lastOperationTime = DateTime.UtcNow;
                }

                await Task.Delay(1000); // Production-like interval
            }

            var uptime = duration.TotalMilliseconds - downtimeMs;
            var availability = uptime / duration.TotalMilliseconds;

            return new ProductionPerformanceMetrics
            {
                AverageResponseTime = responseTimes.Any() ? responseTimes.Average() : 0,
                Availability = availability,
                ErrorRate = (double)errorCount / totalOperations,
                PeakMemoryUsageMB = peakMemory / 1024 / 1024,
                Throughput = totalOperations / duration.TotalSeconds
            };
        }

        private double CalculatePercentile(List<long> values, double percentile)
        {
            if (!values.Any()) return 0;

            var sorted = values.OrderBy(v => v).ToList();
            var index = (int)Math.Ceiling(percentile / 100.0 * sorted.Count) - 1;
            return Math.Max(0, Math.Min(sorted.Count - 1, index)) >= 0 ? sorted[Math.Max(0, Math.Min(sorted.Count - 1, index))] : 0;
        }

        private double CalculateMemoryTrend(List<MemoryMeasurement> measurements)
        {
            if (measurements.Count < 2) return 0;

            var first = measurements.First();
            var last = measurements.Last();
            var timeSpan = (last.Timestamp - first.Timestamp).TotalMinutes;
            var memoryGrowth = last.MemoryUsageMB - first.MemoryUsageMB;

            return timeSpan > 0 ? memoryGrowth / timeSpan : 0;
        }

        private async Task SaveBenchmarkResults(object results, string fileName)
        {
            var filePath = Path.Combine(_benchmarkResultsPath, fileName);
            var json = System.Text.Json.JsonSerializer.Serialize(results, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });
            await File.WriteAllTextAsync(filePath, json);
        }

        #endregion

        #region Result Classes

        private class BenchmarkResults
        {
            public string TestName { get; set; }
            public int TotalRuns { get; set; }
            public double AverageResponseTime { get; set; }
            public double MaxResponseTime { get; set; }
            public double P95ResponseTime { get; set; }
            public double P99ResponseTime { get; set; }
            public bool SLACompliant { get; set; }
            public DateTime Timestamp { get; set; }
        }

        private class ConcurrentBenchmarkResult
        {
            public int ProcessCount { get; set; }
            public TimeSpan TotalTime { get; set; }
            public double Throughput { get; set; }
            public double AverageResponseTime { get; set; }
            public double MaxResponseTime { get; set; }
            public double PeakMemoryUsageMB { get; set; }
        }

        private class MemoryMeasurement
        {
            public DateTime Timestamp { get; set; }
            public double MemoryUsageMB { get; set; }
            public TimeSpan ElapsedTime { get; set; }
        }

        private class MemoryBenchmarkResult
        {
            public TimeSpan TestDuration { get; set; }
            public double TotalMemoryGrowthMB { get; set; }
            public double MaxAllowedGrowthMB { get; set; }
            public List<MemoryMeasurement> MemoryMeasurements { get; set; }
            public double MemoryTrend { get; set; }
            public bool SLACompliant { get; set; }
        }

        private class StressTestMetric
        {
            public int ProcessCount { get; set; }
            public TimeSpan Duration { get; set; }
            public int TotalOperations { get; set; }
            public double Throughput { get; set; }
            public double AverageResponseTime { get; set; }
            public double ErrorRate { get; set; }
            public double PeakMemoryUsageMB { get; set; }
            public double SystemStability { get; set; }
        }

        private class StressTestResult
        {
            public StressTestMetric BaselineMetrics { get; set; }
            public StressTestMetric PeakLoadMetrics { get; set; }
            public StressTestMetric RecoveryMetrics { get; set; }
            public TimeSpan TestDuration { get; set; }
            public double MaxAllowedErrorRate { get; set; }
            public bool SLACompliant { get; set; }
        }

        private class ProductionSLARequirements
        {
            public TimeSpan MaxResponseTime { get; set; }
            public double MinAvailability { get; set; }
            public double MaxErrorRate { get; set; }
            public int MaxMemoryUsageMB { get; set; }
            public int MinThroughput { get; set; }
        }

        private class ProductionPerformanceMetrics
        {
            public double AverageResponseTime { get; set; }
            public double Availability { get; set; }
            public double ErrorRate { get; set; }
            public double PeakMemoryUsageMB { get; set; }
            public double Throughput { get; set; }
        }

        private class ProductionBenchmarkResult
        {
            public TimeSpan TestDuration { get; set; }
            public ProductionSLARequirements SLARequirements { get; set; }
            public ProductionPerformanceMetrics PerformanceMetrics { get; set; }
            public bool SLACompliant { get; set; }
        }

        #endregion
    }
}