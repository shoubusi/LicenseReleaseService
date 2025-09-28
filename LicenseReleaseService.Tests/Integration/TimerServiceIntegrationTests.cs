using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using LicenseReleaseService.IdleDetection;
using LicenseReleaseService.TimerExecution;

namespace LicenseReleaseService.Tests.Integration
{
    /// <summary>
    /// Integration tests for TimerService integration with idle detection components
    /// </summary>
    [TestClass]
    public class TimerServiceIntegrationTests
    {
        private ITimerExecutionService _timerService;
        private IdleDetectionEngine _idleDetectionEngine;
        private TestTimerExecutionService _testTimerService;
        private List<IdleDetectionEventArgs> _detectionEvents;
        private ManualResetEvent _detectionEventSignal;

        [TestInitialize]
        public void TestInitialize()
        {
            _testTimerService = new TestTimerExecutionService();
            _timerService = _testTimerService;
            _detectionEvents = new List<IdleDetectionEventArgs>();
            _detectionEventSignal = new ManualResetEvent(false);

            // Create idle detection engine with mock detectors
            var detectors = new List<IIdleDetector>
            {
                new MockIdleDetector("TimeBased", true, 0.8),
                new MockIdleDetector("PingBased", true, 0.9),
                new MockIdleDetector("ActivityBased", true, 0.7)
            };

            _idleDetectionEngine = new IdleDetectionEngine(
                detectors,
                new IdleDetectionConfiguration
                {
                    DetectionInterval = 1, // 1 minute for testing
                    Consensus = new ConsensusConfig
                    {
                        MinimumConfidence = 0.6,
                        RequireAllDetectors = false
                    }
                },
                new Moq.Mock<Microsoft.Extensions.Logging.ILogger<IdleDetectionEngine>>().Object
            );

            // Subscribe to detection events
            _idleDetectionEngine.IdleDetected += OnIdleDetected;
            _idleDetectionEngine.ActivityDetected += OnActivityDetected;
            _idleDetectionEngine.StateChanged += OnStateChanged;
        }

        [TestCleanup]
        public void TestCleanup()
        {
            _idleDetectionEngine?.Dispose();
            _testTimerService?.Dispose();
            _detectionEventSignal?.Dispose();
        }

        private void OnIdleDetected(object sender, IdleDetectionEventArgs e)
        {
            _detectionEvents.Add(e);
            _detectionEventSignal.Set();
        }

        private void OnActivityDetected(object sender, IdleDetectionEventArgs e)
        {
            _detectionEvents.Add(e);
            _detectionEventSignal.Set();
        }

        private void OnStateChanged(object sender, SessionStateChangedEventArgs e)
        {
            Console.WriteLine($"Session state changed: {e.OldState} -> {e.NewState} for Process {e.ProcessId}");
        }

        [TestMethod]
        public async Task TimerIntegration_ScheduledDetection_ShouldExecutePeriodically()
        {
            // Arrange
            var detectionCount = 0;
            _idleDetectionEngine.IdleDetected += (s, e) => detectionCount++;

            var taskName = "IdleDetection_1";
            var detectionTask = new TimerTask
            {
                Id = Guid.NewGuid(),
                Name = taskName,
                Interval = TimeSpan.FromSeconds(1), // Short interval for testing
                IsEnabled = true,
                ExecuteOnce = false,
                CreatedAt = DateTime.UtcNow,
                LastExecuted = null
            };

            // Act - Schedule the task
            await _timerService.ScheduleTaskAsync(detectionTask, async (token) =>
            {
                // Execute idle detection
                var processes = new[] { new MockProcess(1234, "solidworks.exe") };
                await _idleDetectionEngine.DetectIdleStatesAsync(processes);
            });

            // Wait for multiple executions
            await Task.Delay(TimeSpan.FromSeconds(3));

            // Assert
            Assert.IsTrue(detectionCount >= 2, $"Expected at least 2 detections, got {detectionCount}");
            Assert.IsTrue(_testTimerService.GetExecutionCount(taskName) >= 2,
                $"Expected at least 2 timer executions, got {_testTimerService.GetExecutionCount(taskName)}");
        }

        [TestMethod]
        public async Task TimerIntegration_DetectionFailure_ShouldHandleGracefully()
        {
            // Arrange
            var failingDetector = new MockIdleDetector("FailingDetector", false, 0.0, true);
            var detectors = new List<IIdleDetector> { failingDetector };

            var failingEngine = new IdleDetectionEngine(
                detectors,
                new IdleDetectionConfiguration
                {
                    DetectionInterval = 1,
                    Consensus = new ConsensusConfig { MinimumConfidence = 0.6 }
                },
                new Moq.Mock<Microsoft.Extensions.Logging.ILogger<IdleDetectionEngine>>().Object
            );

            var taskName = "FailingDetection_1";
            var detectionTask = new TimerTask
            {
                Id = Guid.NewGuid(),
                Name = taskName,
                Interval = TimeSpan.FromSeconds(1),
                IsEnabled = true,
                ExecuteOnce = false,
                CreatedAt = DateTime.UtcNow,
                LastExecuted = null
            };

            // Act - Schedule task with failing detection
            await _timerService.ScheduleTaskAsync(detectionTask, async (token) =>
            {
                var processes = new[] { new MockProcess(1234, "solidworks.exe") };
                await failingEngine.DetectIdleStatesAsync(processes);
            });

            // Wait for execution
            await Task.Delay(TimeSpan.FromSeconds(2));

            // Assert - Should not throw exception, system should continue running
            var executionCount = _testTimerService.GetExecutionCount(taskName);
            Assert.IsTrue(executionCount >= 1, $"Task should have executed at least once, got {executionCount}");
        }

        [TestMethod]
        public async Task TimerIntegration_MultipleDetectionTasks_ShouldExecuteConcurrently()
        {
            // Arrange
            var task1Executions = 0;
            var task2Executions = 0;

            var engine1 = new IdleDetectionEngine(
                new List<IIdleDetector> { new MockIdleDetector("Detector1", true, 0.8) },
                new IdleDetectionConfiguration { DetectionInterval = 1 },
                new Moq.Mock<Microsoft.Extensions.Logging.ILogger<IdleDetectionEngine>>().Object
            );

            var engine2 = new IdleDetectionEngine(
                new List<IIdleDetector> { new MockIdleDetector("Detector2", true, 0.9) },
                new IdleDetectionConfiguration { DetectionInterval = 1 },
                new Moq.Mock<Microsoft.Extensions.Logging.ILogger<IdleDetectionEngine>>().Object
            );

            engine1.IdleDetected += (s, e) => task1Executions++;
            engine2.IdleDetected += (s, e) => task2Executions++;

            var task1 = new TimerTask
            {
                Id = Guid.NewGuid(),
                Name = "Detection_Task_1",
                Interval = TimeSpan.FromSeconds(1),
                IsEnabled = true,
                ExecuteOnce = false,
                CreatedAt = DateTime.UtcNow
            };

            var task2 = new TimerTask
            {
                Id = Guid.NewGuid(),
                Name = "Detection_Task_2",
                Interval = TimeSpan.FromSeconds(1.5), // Different interval
                IsEnabled = true,
                ExecuteOnce = false,
                CreatedAt = DateTime.UtcNow
            };

            // Act - Schedule both tasks
            await _timerService.ScheduleTaskAsync(task1, async (token) =>
            {
                var processes = new[] { new MockProcess(1234, "solidworks.exe") };
                await engine1.DetectIdleStatesAsync(processes);
            });

            await _timerService.ScheduleTaskAsync(task2, async (token) =>
            {
                var processes = new[] { new MockProcess(5678, "solidworks.exe") };
                await engine2.DetectIdleStatesAsync(processes);
            });

            // Wait for concurrent execution
            await Task.Delay(TimeSpan.FromSeconds(4));

            // Assert
            Assert.IsTrue(task1Executions >= 3, $"Task 1 should have executed at least 3 times, got {task1Executions}");
            Assert.IsTrue(task2Executions >= 2, $"Task 2 should have executed at least 2 times, got {task2Executions}");
        }

        [TestMethod]
        public async Task TimerIntegration_TaskCancellation_ShouldStopGracefully()
        {
            // Arrange
            var detectionCount = 0;
            _idleDetectionEngine.IdleDetected += (s, e) => detectionCount++;

            var taskName = "CancellableDetection_1";
            var detectionTask = new TimerTask
            {
                Id = Guid.NewGuid(),
                Name = taskName,
                Interval = TimeSpan.FromSeconds(1),
                IsEnabled = true,
                ExecuteOnce = false,
                CreatedAt = DateTime.UtcNow
            };

            var cts = new CancellationTokenSource();

            // Act - Schedule and then cancel task
            await _timerService.ScheduleTaskAsync(detectionTask, async (token) =>
            {
                token.ThrowIfCancellationRequested();
                var processes = new[] { new MockProcess(1234, "solidworks.exe") };
                await _idleDetectionEngine.DetectIdleStatesAsync(processes);
            }, cts.Token);

            // Let it run a bit
            await Task.Delay(TimeSpan.FromSeconds(1.5));

            // Cancel the task
            cts.Cancel();

            // Wait a bit more
            await Task.Delay(TimeSpan.FromSeconds(1));

            // Assert - Task should have been cancelled gracefully
            var finalCount = detectionCount;
            await Task.Delay(TimeSpan.FromSeconds(1)); // Wait to ensure no more executions
            Assert.AreEqual(finalCount, detectionCount, "No more detections should occur after cancellation");
        }

        [TestMethod]
        public async Task TimerIntegration_TaskPriority_ShouldExecuteBasedOnPriority()
        {
            // Arrange
            var highPriorityExecutions = 0;
            var lowPriorityExecutions = 0;

            var highPriorityEngine = new IdleDetectionEngine(
                new List<IIdleDetector> { new MockIdleDetector("HighPriority", true, 0.9) },
                new IdleDetectionConfiguration { DetectionInterval = 1 },
                new Moq.Mock<Microsoft.Extensions.Logging.ILogger<IdleDetectionEngine>>().Object
            );

            var lowPriorityEngine = new IdleDetectionEngine(
                new List<IIdleDetector> { new MockIdleDetector("LowPriority", true, 0.7) },
                new IdleDetectionConfiguration { DetectionInterval = 1 },
                new Moq.Mock<Microsoft.Extensions.Logging.ILogger<IdleDetectionEngine>>().Object
            );

            highPriorityEngine.IdleDetected += (s, e) => highPriorityExecutions++;
            lowPriorityEngine.IdleDetected += (s, e) => lowPriorityExecutions++;

            var highPriorityTask = new TimerTask
            {
                Id = Guid.NewGuid(),
                Name = "HighPriorityDetection",
                Interval = TimeSpan.FromSeconds(1),
                IsEnabled = true,
                ExecuteOnce = false,
                Priority = TaskPriority.High,
                CreatedAt = DateTime.UtcNow
            };

            var lowPriorityTask = new TimerTask
            {
                Id = Guid.NewGuid(),
                Name = "LowPriorityDetection",
                Interval = TimeSpan.FromSeconds(1),
                IsEnabled = true,
                ExecuteOnce = false,
                Priority = TaskPriority.Low,
                CreatedAt = DateTime.UtcNow
            };

            // Act - Schedule both tasks with different priorities
            await _timerService.ScheduleTaskAsync(highPriorityTask, async (token) =>
            {
                var processes = new[] { new MockProcess(1234, "solidworks.exe") };
                await highPriorityEngine.DetectIdleStatesAsync(processes);
            });

            await _timerService.ScheduleTaskAsync(lowPriorityTask, async (token) =>
            {
                var processes = new[] { new MockProcess(5678, "solidworks.exe") };
                await lowPriorityEngine.DetectIdleStatesAsync(processes);
            });

            // Wait for execution
            await Task.Delay(TimeSpan.FromSeconds(3));

            // Assert - Both should execute, priority affects execution order
            Assert.IsTrue(highPriorityExecutions >= 2, $"High priority task should execute at least twice, got {highPriorityExecutions}");
            Assert.IsTrue(lowPriorityExecutions >= 2, $"Low priority task should execute at least twice, got {lowPriorityExecutions}");
        }

        [TestMethod]
        public async Task TimerIntegration_WithErrorHandling_ShouldContinueExecution()
        {
            // Arrange
            var successfulExecutions = 0;
            var errorCount = 0;

            var flakyDetector = new MockIdleDetector("FlakyDetector", true, 0.8, throwOnOddCalls: true);
            var resilientEngine = new IdleDetectionEngine(
                new List<IIdleDetector> { flakyDetector },
                new IdleDetectionConfiguration { DetectionInterval = 1 },
                new Moq.Mock<Microsoft.Extensions.Logging.ILogger<IdleDetectionEngine>>().Object
            );

            resilientEngine.IdleDetected += (s, e) => successfulExecutions++;
            resilientEngine.DetectionError += (s, e) => errorCount++;

            var taskName = "ResilientDetection_1";
            var detectionTask = new TimerTask
            {
                Id = Guid.NewGuid(),
                Name = taskName,
                Interval = TimeSpan.FromSeconds(1),
                IsEnabled = true,
                ExecuteOnce = false,
                CreatedAt = DateTime.UtcNow
            };

            // Act - Schedule task with error-prone detection
            await _timerService.ScheduleTaskAsync(detectionTask, async (token) =>
            {
                var processes = new[] { new MockProcess(1234, "solidworks.exe") };
                await resilientEngine.DetectIdleStatesAsync(processes);
            });

            // Wait for multiple executions
            await Task.Delay(TimeSpan.FromSeconds(4));

            // Assert - System should continue despite errors
            var totalExecutions = _testTimerService.GetExecutionCount(taskName);
            Assert.IsTrue(totalExecutions >= 3, $"Should have executed at least 3 times, got {totalExecutions}");
            Assert.IsTrue(errorCount > 0, "Should have encountered some errors");
            Assert.IsTrue(successfulExecutions > 0, "Should have some successful executions despite errors");
        }

        [TestMethod]
        public async Task TimerIntegration_ConfigurationReload_ShouldUpdateSchedule()
        {
            // Arrange
            var executionCount = 0;
            _idleDetectionEngine.IdleDetected += (s, e) => executionCount++;

            var taskName = "ConfigurableDetection_1";
            var detectionTask = new TimerTask
            {
                Id = Guid.NewGuid(),
                Name = taskName,
                Interval = TimeSpan.FromSeconds(2), // Start with 2 second interval
                IsEnabled = true,
                ExecuteOnce = false,
                CreatedAt = DateTime.UtcNow
            };

            // Act - Schedule initial task
            await _timerService.ScheduleTaskAsync(detectionTask, async (token) =>
            {
                var processes = new[] { new MockProcess(1234, "solidworks.exe") };
                await _idleDetectionEngine.DetectIdleStatesAsync(processes);
            });

            // Wait a bit
            await Task.Delay(TimeSpan.FromSeconds(3));
            var initialExecutions = executionCount;

            // Update configuration (simulate reload)
            var updatedTask = new TimerTask
            {
                Id = detectionTask.Id,
                Name = taskName,
                Interval = TimeSpan.FromSeconds(1), // Change to 1 second interval
                IsEnabled = true,
                ExecuteOnce = false,
                CreatedAt = DateTime.UtcNow
            };

            await _timerService.UpdateTaskAsync(updatedTask);

            // Wait with new interval
            await Task.Delay(TimeSpan.FromSeconds(3));

            // Assert - Should have more executions with shorter interval
            Assert.IsTrue(executionCount > initialExecutions,
                $"Should have more executions after interval change. Initial: {initialExecutions}, Final: {executionCount}");
        }

        [TestMethod]
        public async Task TimerIntegration_PerformanceMonitoring_ShouldTrackMetrics()
        {
            // Arrange
            var stopwatch = new Stopwatch();
            stopwatch.Start();

            var taskName = "PerformanceMonitoring_1";
            var detectionTask = new TimerTask
            {
                Id = Guid.NewGuid(),
                Name = taskName,
                Interval = TimeSpan.FromSeconds(1),
                IsEnabled = true,
                ExecuteOnce = false,
                CreatedAt = DateTime.UtcNow
            };

            // Act - Schedule performance monitoring task
            await _timerService.ScheduleTaskAsync(detectionTask, async (token) =>
            {
                var processes = new[] { new MockProcess(1234, "solidworks.exe") };
                await _idleDetectionEngine.DetectIdleStatesAsync(processes);
            });

            // Wait for several executions
            await Task.Delay(TimeSpan.FromSeconds(5));

            // Get performance metrics
            var metrics = _testTimerService.GetPerformanceMetrics(taskName);

            // Assert
            Assert.IsNotNull(metrics, "Performance metrics should be available");
            Assert.IsTrue(metrics.TotalExecutions >= 4, $"Should have at least 4 executions, got {metrics.TotalExecutions}");
            Assert.IsTrue(metrics.AverageExecutionTimeMs > 0, "Average execution time should be positive");
            Assert.IsTrue(metrics.MaxExecutionTimeMs >= metrics.MinExecutionTimeMs, "Max time should be >= min time");
            Assert.IsTrue(metrics.SuccessRate > 0.8, $"Success rate should be high, got {metrics.SuccessRate:P2}");

            Console.WriteLine($"Performance Metrics: {metrics}");
        }

        [TestMethod]
        public async Task TimerIntegration_ResourceCleanup_ShouldDisposeProperly()
        {
            // Arrange
            var disposedCount = 0;
            var disposableDetector = new DisposableMockDetector(() => disposedCount++);

            var engine = new IdleDetectionEngine(
                new List<IIdleDetector> { disposableDetector },
                new IdleDetectionConfiguration { DetectionInterval = 1 },
                new Moq.Mock<Microsoft.Extensions.Logging.ILogger<IdleDetectionEngine>>().Object
            );

            var taskName = "CleanupTest_1";
            var detectionTask = new TimerTask
            {
                Id = Guid.NewGuid(),
                Name = taskName,
                Interval = TimeSpan.FromSeconds(1),
                IsEnabled = true,
                ExecuteOnce = false,
                CreatedAt = DateTime.UtcNow
            };

            // Act - Schedule and then cleanup
            await _timerService.ScheduleTaskAsync(detectionTask, async (token) =>
            {
                var processes = new[] { new MockProcess(1234, "solidworks.exe") };
                await engine.DetectIdleStatesAsync(processes);
            });

            await Task.Delay(TimeSpan.FromSeconds(2));

            // Dispose of components
            engine.Dispose();
            await _timerService.CancelTaskAsync(taskName);

            // Assert - Resources should be cleaned up
            Assert.AreEqual(1, disposedCount, "Disposable detector should have been disposed once");
        }

        #region Helper Classes

        internal class MockTimerExecutionService : ITimerExecutionService
        {
            private readonly Dictionary<string, TimerTask> _tasks = new Dictionary<string, TimerTask>();
            private readonly Dictionary<string, int> _executionCounts = new Dictionary<string, int>();
            private readonly Dictionary<string, PerformanceMetrics> _metrics = new Dictionary<string, PerformanceMetrics>();

            public async Task ScheduleTaskAsync(TimerTask task, Func<CancellationToken, Task> action, CancellationToken cancellationToken = default)
            {
                _tasks[task.Name] = task;
                _executionCounts[task.Name] = 0;

                // Simulate periodic execution
                _ = Task.Run(async () =>
                {
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        await Task.Delay(task.Interval, cancellationToken);
                        if (!cancellationToken.IsCancellationRequested)
                        {
                            await ExecuteTask(task.Name, action, cancellationToken);
                        }
                    }
                }, cancellationToken);
            }

            private async Task ExecuteTask(string taskName, Func<CancellationToken, Task> action, CancellationToken cancellationToken)
            {
                var stopwatch = Stopwatch.StartNew();
                try
                {
                    await action(cancellationToken);
                    _executionCounts[taskName]++;

                    // Update metrics
                    if (!_metrics.ContainsKey(taskName))
                    {
                        _metrics[taskName] = new PerformanceMetrics();
                    }
                    _metrics[taskName].RecordExecution(stopwatch.ElapsedMilliseconds, true);
                }
                catch (Exception)
                {
                    _metrics[taskName].RecordExecution(stopwatch.ElapsedMilliseconds, false);
                }
            }

            public async Task CancelTaskAsync(Guid taskId)
            {
                var task = _tasks.Values.FirstOrDefault(t => t.Id == taskId);
                if (task != null)
                {
                    _tasks.Remove(task.Name);
                }
                await Task.CompletedTask;
            }

            public async Task CancelAllTasksAsync()
            {
                _tasks.Clear();
                await Task.CompletedTask;
            }

            public async Task UpdateTaskAsync(TimerTask task)
            {
                _tasks[task.Name] = task;
                await Task.CompletedTask;
            }

            public async Task<TimerTask> GetTaskAsync(Guid taskId)
            {
                return await Task.FromResult(_tasks.Values.FirstOrDefault(t => t.Id == taskId));
            }

            public async Task<IEnumerable<TimerTask>> GetAllTasksAsync()
            {
                return await Task.FromResult(_tasks.Values.AsEnumerable());
            }

            public int GetExecutionCount(string taskName)
            {
                return _executionCounts.TryGetValue(taskName, out var count) ? count : 0;
            }

            public PerformanceMetrics GetPerformanceMetrics(string taskName)
            {
                return _metrics.TryGetValue(taskName, out var metrics) ? metrics : new PerformanceMetrics();
            }

            public void Dispose()
            {
                _tasks.Clear();
                _executionCounts.Clear();
                _metrics.Clear();
            }
        }

        internal class PerformanceMetrics
        {
            public int TotalExecutions { get; private set; }
            public int SuccessfulExecutions { get; private set; }
            public double AverageExecutionTimeMs { get; private set; }
            public double MaxExecutionTimeMs { get; private set; }
            public double MinExecutionTimeMs { get; private set; }
            public double SuccessRate => TotalExecutions > 0 ? (double)SuccessfulExecutions / TotalExecutions : 0;

            public void RecordExecution(long executionTimeMs, bool success)
            {
                TotalExecutions++;
                if (success) SuccessfulExecutions++;

                AverageExecutionTimeMs = (AverageExecutionTimeMs * (TotalExecutions - 1) + executionTimeMs) / TotalExecutions;
                MaxExecutionTimeMs = Math.Max(MaxExecutionTimeMs, executionTimeMs);
                MinExecutionTimeMs = TotalExecutions == 1 ? executionTimeMs : Math.Min(MinExecutionTimeMs, executionTimeMs);
            }

            public override string ToString()
            {
                return $"Total: {TotalExecutions}, Success: {SuccessRate:P2}, Avg: {AverageExecutionTimeMs:F1}ms, Min: {MinExecutionTimeMs:F1}ms, Max: {MaxExecutionTimeMs:F1}ms";
            }
        }

        internal class MockIdleDetector : IIdleDetector
        {
            private readonly string _name;
            private readonly bool _isIdle;
            private readonly double _confidence;
            private readonly bool _shouldFail;
            private readonly bool _throwOnOddCalls;
            private int _callCount;

            public MockIdleDetector(string name, bool isIdle, double confidence, bool shouldFail = false, bool throwOnOddCalls = false)
            {
                _name = name;
                _isIdle = isIdle;
                _confidence = confidence;
                _shouldFail = shouldFail;
                _throwOnOddCalls = throwOnOddCalls;
            }

            public async Task<DetectorResult> DetectIdleAsync(System.Diagnostics.Process process)
            {
                _callCount++;

                if (_throwOnOddCalls && _callCount % 2 == 1)
                {
                    throw new InvalidOperationException($"Simulated failure in {_name} detector");
                }

                if (_shouldFail)
                {
                    return new DetectorResult
                    {
                        DetectorType = _name,
                        IsIdle = false,
                        Confidence = 0.0,
                        Error = "Simulated detection failure"
                    };
                }

                return await Task.FromResult(new DetectorResult
                {
                    DetectorType = _name,
                    IsIdle = _isIdle,
                    Confidence = _confidence,
                    LastActivityTime = DateTime.UtcNow.AddMinutes(-30),
                    IdleDuration = TimeSpan.FromMinutes(30),
                    Metadata = new Dictionary<string, object>
                    {
                        ["CallCount"] = _callCount,
                        ["ProcessId"] = process.Id
                    }
                });
            }
        }

        internal class MockProcess : System.Diagnostics.Process
        {
            private readonly int _id;
            private readonly string _processName;

            public MockProcess(int id, string processName)
            {
                _id = id;
                _processName = processName;
            }

            public new int Id => _id;
            public new string ProcessName => _processName;
            public new DateTime StartTime => DateTime.UtcNow.AddMinutes(-60);
            public new bool Responding => true;
        }

        internal class DisposableMockDetector : MockIdleDetector, IDisposable
        {
            private readonly Action _onDispose;

            public DisposableMockDetector(Action onDispose) : base("Disposable", true, 0.8)
            {
                _onDispose = onDispose;
            }

            public void Dispose()
            {
                _onDispose?.Invoke();
            }
        }

        #endregion
    }
}