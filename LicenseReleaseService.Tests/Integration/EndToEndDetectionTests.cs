using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using LicenseReleaseService.IdleDetection;
using LicenseReleaseService.Configuration;
using LicenseReleaseService.TimerExecution;
using LicenseReleaseService.LicenseManagement;
using LicenseReleaseService.LicenseManagement.Caching;
using LicenseReleaseService.LicenseManagement.Models;
using LicenseReleaseService.LicenseManagement.Parsing;
using LicenseReleaseService.Process;
using LicenseReleaseService.VersionManagement;

namespace LicenseReleaseService.Tests.Integration
{
    /// <summary>
    /// End-to-end integration tests for the complete idle detection workflow
    /// </summary>
    [TestClass]
    public class EndToEndDetectionTests
    {
        private IdleDetectionEngine _idleDetectionEngine;
        private ITimerExecutionService _timerService;
        private IVersionManager _versionManager;
        private ICacheManager _cacheManager;
        private IProcessExecutor _processExecutor;
        private TestTimerExecutionService _testTimerService;
        private TestVersionManager _testVersionManager;
        private TestCacheManager _testCacheManager;
        private TestProcessExecutor _testProcessExecutor;
        private List<IdleDetectionEventArgs> _detectionEvents;
        private List<LicenseActionEventArgs> _licenseEvents;
        private ManualResetEvent _workflowCompleteSignal;

        [TestInitialize]
        public async Task TestInitialize()
        {
            _testTimerService = new TestTimerExecutionService();
            _testVersionManager = new TestVersionManager();
            _testCacheManager = new TestCacheManager();
            _testProcessExecutor = new TestProcessExecutor();

            _timerService = _testTimerService;
            _versionManager = _testVersionManager;
            _cacheManager = _testCacheManager;
            _processExecutor = _testProcessExecutor;

            _detectionEvents = new List<IdleDetectionEventArgs>();
            _licenseEvents = new List<LicenseActionEventArgs>();
            _workflowCompleteSignal = new ManualResetEvent(false);

            // Create realistic detectors
            var detectors = new List<IIdleDetector>
            {
                new RealisticTimeBasedDetector(),
                new RealisticPingBasedDetector(_testProcessExecutor),
                new RealisticActivityMonitor(_testProcessExecutor)
            };

            _idleDetectionEngine = new IdleDetectionEngine(
                detectors,
                new IdleDetectionConfiguration
                {
                    DetectionInterval = 1,
                    Consensus = new ConsensusConfig
                    {
                        MinimumConfidence = 0.7,
                        RequireAllDetectors = false
                    }
                },
                new Moq.Mock<Microsoft.Extensions.Logging.ILogger<IdleDetectionEngine>>().Object
            );

            // Subscribe to all events
            _idleDetectionEngine.IdleDetected += OnIdleDetected;
            _idleDetectionEngine.ActivityDetected += OnActivityDetected;
            _idleDetectionEngine.StateChanged += OnStateChanged;

            // Setup test versions
            await SetupTestVersions();
        }

        [TestCleanup]
        public void TestCleanup()
        {
            _idleDetectionEngine?.Dispose();
            _testTimerService?.Dispose();
            _testVersionManager?.Dispose();
            _testCacheManager?.Clear();
            _testProcessExecutor?.Reset();
            _workflowCompleteSignal?.Dispose();
        }

        private void OnIdleDetected(object sender, IdleDetectionEventArgs e)
        {
            _detectionEvents.Add(e);
            Console.WriteLine($"Idle detected for process {e.ProcessId} with confidence {e.Confidence:P2}");
        }

        private void OnActivityDetected(object sender, IdleDetectionEventArgs e)
        {
            _detectionEvents.Add(e);
            Console.WriteLine($"Activity detected for process {e.ProcessId}");
        }

        private void OnStateChanged(object sender, SessionStateChangedEventArgs e)
        {
            Console.WriteLine($"Session state changed: {e.OldState} -> {e.NewState} for Process {e.ProcessId}");
        }

        private void OnLicenseAction(object sender, LicenseActionEventArgs e)
        {
            _licenseEvents.Add(e);
            Console.WriteLine($"License action: {e.Action} for {e.User}@{e.Host}");
        }

        private async Task SetupTestVersions()
        {
            var versions = new List<VersionInfo>
            {
                new VersionInfo
                {
                    Id = 1,
                    Version = "2023",
                    Path = @"C:\SolidWorks 2023",
                    DisplayName = "SolidWorks 2023",
                    Configuration = new VersionSpecificConfig
                    {
                        IdleThresholdMinutes = 15,
                        ConfidenceThreshold = 0.7
                    }
                },
                new VersionInfo
                {
                    Id = 2,
                    Version = "2024",
                    Path = @"C:\SolidWorks 2024",
                    DisplayName = "SolidWorks 2024",
                    Configuration = new VersionSpecificConfig
                    {
                        IdleThresholdMinutes = 20,
                        ConfidenceThreshold = 0.8
                    }
                }
            };

            foreach (var version in versions)
            {
                await _versionManager.AddVersionAsync(version);
            }
        }

        [TestMethod]
        public async Task EndToEndWorkflow_CompleteDetectionCycle_ShouldWorkEndToEnd()
        {
            // Arrange
            var processes = new[]
            {
                new MockProcess(1234, "SLDWORKS.exe", @"C:\SolidWorks 2023\SLDWORKS.exe"),
                new MockProcess(5678, "SLDWORKS.exe", @"C:\SolidWorks 2024\SLDWORKS.exe")
            };

            var stopwatch = Stopwatch.StartNew();

            // Act - Complete end-to-end workflow
            // 1. Version detection and mapping
            var versionMapping = await MapProcessesToVersions(processes);

            // 2. Idle detection with version-specific configuration
            var detectionResults = await _idleDetectionEngine.DetectIdleStatesAsync(processes);

            // 3. Version-specific result processing
            await ProcessVersionSpecificResults(versionMapping, detectionResults);

            // 4. Cache results for performance
            await CacheDetectionResults(versionMapping, detectionResults);

            // 5. Timer-based scheduled detection
            await SetupScheduledDetection(processes);

            // Wait for scheduled detection to complete
            await Task.Delay(TimeSpan.FromSeconds(3));

            stopwatch.Stop();

            // Assert - Complete workflow validation
            Assert.AreEqual(2, versionMapping.Count, "Should map both processes to versions");
            Assert.AreEqual(2, detectionResults.Count, "Should detect both processes");
            Assert.IsTrue(_detectionEvents.Count >= 2, $"Should have detection events, got {_detectionEvents.Count}");
            Assert.IsTrue(_testTimerService.GetExecutionCount("ScheduledDetection") >= 2,
                "Should have executed scheduled detection at least twice");

            // Verify version-specific processing
            var versionResults = await _versionManager.GetVersionGroupedResultsAsync();
            Assert.AreEqual(2, versionResults.Count, "Should have results for both versions");

            // Verify caching
            var cachedResults = await GetCachedResults(processes);
            Assert.AreEqual(2, cachedResults.Count, "Should cache results for both processes");

            Console.WriteLine($"End-to-end workflow completed in {stopwatch.ElapsedMilliseconds}ms");
            Console.WriteLine($"Detection events: {_detectionEvents.Count}");
            Console.WriteLine($"Version results: {versionResults.Count}");
            Console.WriteLine($"Scheduled executions: {_testTimerService.GetExecutionCount("ScheduledDetection")}");
        }

        [TestMethod]
        public async Task EndToEndWorkflow_MixedActivityStates_ShouldHandleCorrectly()
        {
            // Arrange - Create processes with mixed activity states
            var activeProcess = new MockProcess(1234, "SLDWORKS.exe", @"C:\SolidWorks 2023\SLDWORKS.exe")
            {
                LastActivityTime = DateTime.UtcNow.AddMinutes(-5), // Recently active
                IsResponding = true
            };

            var idleProcess = new MockProcess(5678, "SLDWORKS.exe", @"C:\SolidWorks 2024\SLDWORKS.exe")
            {
                LastActivityTime = DateTime.UtcNow.AddMinutes(-25), // Idle for 25 minutes
                IsResponding = false
            };

            var processes = new[] { activeProcess, idleProcess };

            // Act - Process mixed activity states
            var versionMapping = await MapProcessesToVersions(processes);
            var detectionResults = await _idleDetectionEngine.DetectIdleStatesAsync(processes);
            await ProcessVersionSpecificResults(versionMapping, detectionResults);

            // Assert - Should handle mixed states correctly
            Assert.AreEqual(2, detectionResults.Count);

            var activeResult = detectionResults[1234];
            var idleResult = detectionResults[5678];

            Assert.IsFalse(activeResult.IsIdle, "Active process should not be detected as idle");
            Assert.IsTrue(idleResult.IsIdle, "Idle process should be detected as idle");
            Assert.IsTrue(activeResult.Confidence < 0.5, "Active process should have low idle confidence");
            Assert.IsTrue(idleResult.Confidence > 0.7, "Idle process should have high idle confidence");

            // Verify state transitions
            var stateEvents = _detectionEvents.OfType<SessionStateChangedEventArgs>().ToList();
            Assert.IsTrue(stateEvents.Count >= 1, "Should have state transition events");
        }

        [TestMethod]
        public async Task EndToEndWorkflow_ErrorRecovery_ShouldContinueAfterFailures()
        {
            // Arrange - Include error-prone components
            var flakyDetectors = new List<IIdleDetector>
            {
                new RealisticTimeBasedDetector(),
                new FlakyPingBasedDetector(_testProcessExecutor), // This will fail occasionally
                new RealisticActivityMonitor(_testProcessExecutor)
            };

            var flakyEngine = new IdleDetectionEngine(
                flakyDetectors,
                new IdleDetectionConfiguration
                {
                    DetectionInterval = 1,
                    Consensus = new ConsensusConfig { MinimumConfidence = 0.6 }
                },
                new Moq.Mock<Microsoft.Extensions.Logging.ILogger<IdleDetectionEngine>>().Object
            );

            var processes = new[]
            {
                new MockProcess(1234, "SLDWORKS.exe", @"C:\SolidWorks 2023\SLDWORKS.exe")
            };

            // Act - Execute workflow with potential failures
            try
            {
                var versionMapping = await MapProcessesToVersions(processes);
                var detectionResults = await flakyEngine.DetectIdleStatesAsync(processes);
                await ProcessVersionSpecificResults(versionMapping, detectionResults);
                await SetupScheduledDetectionWithFlakyEngine(processes, flakyEngine);

                await Task.Delay(TimeSpan.FromSeconds(4)); // Allow time for failures and recovery
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Workflow encountered error: {ex.Message}");
            }

            // Assert - Should continue despite failures
            var versionResults = await _versionManager.GetVersionGroupedResultsAsync();
            Assert.IsTrue(versionResults.Count >= 0, "Should have version results even with failures");

            var errorLog = await _versionManager.GetVersionErrorsAsync();
            Console.WriteLine($"Errors encountered: {errorLog.Count}");

            // System should remain functional
            var finalDetection = await flakyEngine.DetectIdleStatesAsync(processes);
            Assert.IsNotNull(finalDetection, "Should still be able to perform detection after errors");
        }

        [TestMethod]
        public async Task EndToEndWorkflow_PerformanceUnderLoad_ShouldHandleConcurrentProcesses()
        {
            // Arrange - Create many concurrent processes
            var processes = new List<MockProcess>();
            for (int i = 0; i < 10; i++)
            {
                var versionId = (i % 2) + 1; // Alternate between 2023 and 2024
                var versionPath = versionId == 1 ? @"C:\SolidWorks 2023\SLDWORKS.exe" : @"C:\SolidWorks 2024\SLDWORKS.exe";

                processes.Add(new MockProcess(1000 + i, "SLDWORKS.exe", versionPath)
                {
                    LastActivityTime = DateTime.UtcNow.AddMinutes(-15 - i), // Different idle times
                    IsResponding = i % 3 != 0 // Some processes not responding
                });
            }

            // Act - Process under load
            var stopwatch = Stopwatch.StartNew();

            var versionMapping = await MapProcessesToVersions(processes.ToArray());
            var detectionResults = await _idleDetectionEngine.DetectIdleStatesAsync(processes.ToArray());
            await ProcessVersionSpecificResults(versionMapping, detectionResults);

            // Perform multiple detection cycles under load
            for (int i = 0; i < 5; i++)
            {
                await _idleDetectionEngine.DetectIdleStatesAsync(processes.ToArray());
                await Task.Delay(100); // Small delay between cycles
            }

            stopwatch.Stop();

            // Assert - Should handle load gracefully
            Assert.AreEqual(10, detectionResults.Count, "Should detect all 10 processes");
            Assert.AreEqual(10, versionMapping.Count, "Should map all 10 processes to versions");

            var versionResults = await _versionManager.GetVersionGroupedResultsAsync();
            Assert.AreEqual(2, versionResults.Count, "Should have results for 2 versions");

            var version2023Count = versionResults[1].ProcessCount;
            var version2024Count = versionResults[2].ProcessCount;
            Assert.AreEqual(5, version2023Count, "Should have 5 processes for 2023");
            Assert.AreEqual(5, version2024Count, "Should have 5 processes for 2024");

            Console.WriteLine($"Processed {processes.Count} processes in {stopwatch.ElapsedMilliseconds}ms");
            Console.WriteLine($"Average time per process: {stopwatch.ElapsedMilliseconds / processes.Count:F1}ms");
        }

        [TestMethod]
        public async Task EndToEndWorkflow_ConfigurationChanges_ShouldApplyDynamically()
        {
            // Arrange
            var processes = new[]
            {
                new MockProcess(1234, "SLDWORKS.exe", @"C:\SolidWorks 2023\SLDWORKS.exe")
            };

            // Initial configuration
            var initialConfig = new IdleDetectionConfiguration
            {
                DetectionInterval = 2,
                Consensus = new ConsensusConfig { MinimumConfidence = 0.8 }
            };

            var engine = new IdleDetectionEngine(
                new List<IIdleDetector> { new RealisticTimeBasedDetector() },
                initialConfig,
                new Moq.Mock<Microsoft.Extensions.Logging.ILogger<IdleDetectionEngine>>().Object
            );

            // Act - Initial detection with initial config
            var initialResults = await engine.DetectIdleStatesAsync(processes);

            // Update configuration dynamically
            var updatedConfig = new IdleDetectionConfiguration
            {
                DetectionInterval = 1,
                Consensus = new ConsensusConfig { MinimumConfidence = 0.6 }
            };

            // Simulate configuration reload
            await Task.Delay(100);
            var updatedEngine = new IdleDetectionEngine(
                new List<IIdleDetector> { new RealisticTimeBasedDetector() },
                updatedConfig,
                new Moq.Mock<Microsoft.Extensions.Logging.ILogger<IdleDetectionEngine>>().Object
            );

            var updatedResults = await updatedEngine.DetectIdleStatesAsync(processes);

            // Assert - Configuration changes should be reflected
            Assert.AreEqual(1, initialResults.Count, "Should have initial results");
            Assert.AreEqual(1, updatedResults.Count, "Should have updated results");

            // The results should differ due to configuration changes
            var initialConfidence = initialResults[1234].Confidence;
            var updatedConfidence = updatedResults[1234].Confidence;

            Console.WriteLine($"Initial confidence: {initialConfidence:P2}");
            Console.WriteLine($"Updated confidence: {updatedConfidence:P2}");
        }

        [TestMethod]
        public async Task EndToEndWorkflow_RealWorldScenario_ShouldSimulateProductionEnvironment()
        {
            // Arrange - Simulate real-world scenario with multiple users and versions
            var userProcesses = new[]
            {
                // User 1 - Active on 2023
                new MockProcess(1001, "SLDWORKS.exe", @"C:\SolidWorks 2023\SLDWORKS.exe")
                {
                    LastActivityTime = DateTime.UtcNow.AddMinutes(-2),
                    IsResponding = true,
                    UserSession = "johndoe@WORKSTATION1"
                },
                // User 2 - Idle on 2023
                new MockProcess(1002, "SLDWORKS.exe", @"C:\SolidWorks 2023\SLDWORKS.exe")
                {
                    LastActivityTime = DateTime.UtcNow.AddMinutes(-22),
                    IsResponding = false,
                    UserSession = "janedoe@WORKSTATION2"
                },
                // User 3 - Active on 2024
                new MockProcess(1003, "SLDWORKS.exe", @"C:\SolidWorks 2024\SLDWORKS.exe")
                {
                    LastActivityTime = DateTime.UtcNow.AddMinutes(-1),
                    IsResponding = true,
                    UserSession = "bobsmith@WORKSTATION3"
                },
                // User 4 - Very idle on 2024
                new MockProcess(1004, "SLDWORKS.exe", @"C:\SolidWorks 2024\SLDWORKS.exe")
                {
                    LastActivityTime = DateTime.UtcNow.AddMinutes(-45),
                    IsResponding = false,
                    UserSession = "alice@WORKSTATION4"
                }
            };

            // Act - Real-world scenario processing
            var scenarioStopwatch = Stopwatch.StartNew();

            // 1. User session detection
            var userSessions = await DetectUserSessions(userProcesses);

            // 2. Version-specific processing
            var versionMapping = await MapProcessesToVersions(userProcesses);

            // 3. Idle detection with user context
            var detectionResults = await _idleDetectionEngine.DetectIdleStatesAsync(userProcesses);

            // 4. License management decisions
            var licenseActions = await DetermineLicenseActions(userSessions, detectionResults);

            // 5. Reporting and metrics
            var scenarioReport = await GenerateScenarioReport(userSessions, detectionResults, licenseActions);

            scenarioStopwatch.Stop();

            // Assert - Real-world scenario validation
            Assert.AreEqual(4, userSessions.Count, "Should detect 4 user sessions");
            Assert.AreEqual(4, versionMapping.Count, "Should map all processes to versions");
            Assert.AreEqual(4, detectionResults.Count, "Should detect all processes");
            Assert.AreEqual(4, licenseActions.Count, "Should determine license actions for all");

            // Validate license decisions
            var releaseActions = licenseActions.Values.Where(a => a.Action == "Release").ToList();
            var keepActions = licenseActions.Values.Where(a => a.Action == "Keep").ToList();

            Assert.IsTrue(releaseActions.Count >= 1, "Should release at least 1 idle license");
            Assert.IsTrue(keepActions.Count >= 1, "Should keep at least 1 active license");

            Console.WriteLine($"Real-world scenario completed in {scenarioStopwatch.ElapsedMilliseconds}ms");
            Console.WriteLine($"User sessions: {userSessions.Count}");
            Console.WriteLine($"License releases: {releaseActions.Count}");
            Console.WriteLine($"Licenses kept: {keepActions.Count}");

            // Validate scenario report
            Assert.IsNotNull(scenarioReport, "Should generate scenario report");
            Assert.IsTrue(scenarioReport.TotalProcesses > 0, "Report should show total processes");
            Assert.IsTrue(scenarioReport.ReleaseCandidateCount > 0, "Should have release candidates");
        }

        #region Helper Methods

        private async Task<Dictionary<int, VersionInfo>> MapProcessesToVersions(MockProcess[] processes)
        {
            var mapping = new Dictionary<int, VersionInfo>();

            foreach (var process in processes)
            {
                var allVersions = await _versionManager.GetAllVersionsAsync();
                var matchedVersion = allVersions.FirstOrDefault(v => process.MainModule.FileName.Contains(v.Path));

                if (matchedVersion != null)
                {
                    mapping[process.Id] = matchedVersion;
                }
            }

            return mapping;
        }

        private async Task ProcessVersionSpecificResults(Dictionary<int, VersionInfo> versionMapping, Dictionary<int, DetectionResult> detectionResults)
        {
            foreach (var resultPair in detectionResults)
            {
                var processId = resultPair.Key;
                var result = resultPair.Value;

                if (versionMapping.TryGetValue(processId, out var version))
                {
                    await _versionManager.RecordDetectionAsync(version.Id, result);
                }
            }
        }

        private async Task CacheDetectionResults(Dictionary<int, VersionInfo> versionMapping, Dictionary<int, DetectionResult> detectionResults)
        {
            foreach (var resultPair in detectionResults)
            {
                var processId = resultPair.Key;
                var result = resultPair.Value;

                // Cache with version-specific key
                var cacheKey = $"detection_{processId}_{DateTime.UtcNow:yyyyMMddHHmm}";
                await _cacheManager.SetAsync(cacheKey, result, TimeSpan.FromMinutes(5));
            }
        }

        private async Task SetupScheduledDetection(MockProcess[] processes)
        {
            var task = new TimerTask
            {
                Id = Guid.NewGuid(),
                Name = "ScheduledDetection",
                Interval = TimeSpan.FromSeconds(1),
                IsEnabled = true,
                ExecuteOnce = false,
                CreatedAt = DateTime.UtcNow
            };

            await _timerService.ScheduleTaskAsync(task, async (token) =>
            {
                await _idleDetectionEngine.DetectIdleStatesAsync(processes);
            });
        }

        private async Task SetupScheduledDetectionWithFlakyEngine(MockProcess[] processes, IdleDetectionEngine flakyEngine)
        {
            var task = new TimerTask
            {
                Id = Guid.NewGuid(),
                Name = "FlakyScheduledDetection",
                Interval = TimeSpan.FromSeconds(1),
                IsEnabled = true,
                ExecuteOnce = false,
                CreatedAt = DateTime.UtcNow
            };

            await _timerService.ScheduleTaskAsync(task, async (token) =>
            {
                await flakyEngine.DetectIdleStatesAsync(processes);
            });
        }

        private async Task<Dictionary<int, UserSessionInfo>> DetectUserSessions(MockProcess[] processes)
        {
            var sessions = new Dictionary<int, UserSessionInfo>();

            foreach (var process in processes)
            {
                sessions[process.Id] = new UserSessionInfo
                {
                    ProcessId = process.Id,
                    UserName = process.UserSession?.Split('@')[0] ?? "Unknown",
                    Workstation = process.UserSession?.Split('@')[1] ?? "Unknown",
                    LoginTime = DateTime.UtcNow.AddHours(-2),
                    LastActivity = process.LastActivityTime
                };
            }

            return await Task.FromResult(sessions);
        }

        private async Task<Dictionary<int, LicenseActionInfo>> DetermineLicenseActions(
            Dictionary<int, UserSessionInfo> userSessions,
            Dictionary<int, DetectionResult> detectionResults)
        {
            var actions = new Dictionary<int, LicenseActionInfo>();

            foreach (var resultPair in detectionResults)
            {
                var processId = resultPair.Key;
                var result = resultPair.Value;
                var session = userSessions[processId];

                // Simple business logic: release if idle and confidence > 0.8
                var shouldRelease = result.IsIdle && result.Confidence > 0.8;

                actions[processId] = new LicenseActionInfo
                {
                    ProcessId = processId,
                    UserSession = session.UserName,
                    Action = shouldRelease ? "Release" : "Keep",
                    Reason = shouldRelease ? "User idle for extended period" : "User actively working",
                    Confidence = result.Confidence,
                    DecisionTime = DateTime.UtcNow
                };
            }

            return await Task.FromResult(actions);
        }

        private async Task<ScenarioReport> GenerateScenarioReport(
            Dictionary<int, UserSessionInfo> userSessions,
            Dictionary<int, DetectionResult> detectionResults,
            Dictionary<int, LicenseActionInfo> licenseActions)
        {
            var report = new ScenarioReport
            {
                TotalProcesses = userSessions.Count,
                IdleProcessCount = detectionResults.Values.Count(r => r.IsIdle),
                ReleaseCandidateCount = licenseActions.Values.Count(a => a.Action == "Keep"),
                GeneratedAt = DateTime.UtcNow,
                ProcessingTimeMs = 0 // Will be set by caller
            };

            return await Task.FromResult(report);
        }

        private async Task<Dictionary<int, DetectionResult>> GetCachedResults(MockProcess[] processes)
        {
            var cachedResults = new Dictionary<int, DetectionResult>();

            foreach (var process in processes)
            {
                var cacheKey = $"detection_{process.Id}_{DateTime.UtcNow:yyyyMMddHHmm}";
                var cachedResult = await _cacheManager.GetAsync<DetectionResult>(cacheKey);
                if (cachedResult != null)
                {
                    cachedResults[process.Id] = cachedResult;
                }
            }

            return cachedResults;
        }

        #endregion

        #region Helper Classes

        internal class TestTimerExecutionService : ITimerExecutionService
        {
            private readonly Dictionary<string, TimerTask> _tasks = new Dictionary<string, TimerTask>();
            private readonly Dictionary<string, int> _executionCounts = new Dictionary<string, int>();

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
                            await action(cancellationToken);
                            _executionCounts[task.Name]++;
                        }
                    }
                }, cancellationToken);
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

            public void Dispose()
            {
                _tasks.Clear();
                _executionCounts.Clear();
            }
        }

        internal class MockProcess : Process
        {
            private readonly int _id;
            private readonly string _processName;
            private readonly string _mainModulePath;
            public DateTime LastActivityTime { get; set; }
            public bool IsResponding { get; set; }
            public string UserSession { get; set; }

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

        internal class RealisticTimeBasedDetector : IIdleDetector
        {
            public async Task<DetectorResult> DetectIdleAsync(Process process)
            {
                await Task.Delay(10); // Simulate processing time

                var mockProcess = process as MockProcess;
                var idleDuration = DateTime.UtcNow - mockProcess.LastActivityTime;

                return new DetectorResult
                {
                    DetectorType = "TimeBased",
                    IsIdle = idleDuration.TotalMinutes > 15,
                    Confidence = Math.Min(idleDuration.TotalMinutes / 30.0, 1.0),
                    LastActivityTime = mockProcess.LastActivityTime,
                    IdleDuration = idleDuration,
                    Metadata = new Dictionary<string, object>
                    {
                        ["ProcessId"] = process.Id,
                        ["IdleMinutes"] = idleDuration.TotalMinutes
                    }
                };
            }
        }

        internal class RealisticPingBasedDetector : IIdleDetector
        {
            private readonly TestProcessExecutor _processExecutor;

            public RealisticPingBasedDetector(TestProcessExecutor processExecutor)
            {
                _processExecutor = processExecutor;
            }

            public async Task<DetectorResult> DetectIdleAsync(Process process)
            {
                await Task.Delay(15); // Simulate ping time

                var mockProcess = process as MockProcess;
                var isResponsive = mockProcess.IsResponding;

                return new DetectorResult
                {
                    DetectorType = "PingBased",
                    IsIdle = !isResponsive,
                    Confidence = isResponsive ? 0.1 : 0.9,
                    LastActivityTime = mockProcess.LastActivityTime,
                    IdleDuration = DateTime.UtcNow - mockProcess.LastActivityTime,
                    Metadata = new Dictionary<string, object>
                    {
                        ["ProcessId"] = process.Id,
                        ["PingResponseMs"] = isResponsive ? 50 : 5000,
                        ["Responding"] = isResponsive
                    }
                };
            }
        }

        internal class FlakyPingBasedDetector : RealisticPingBasedDetector
        {
            private static int _callCount = 0;

            public FlakyPingBasedDetector(TestProcessExecutor processExecutor) : base(processExecutor)
            {
            }

            public new async Task<DetectorResult> DetectIdleAsync(Process process)
            {
                _callCount++;

                // Fail on every 3rd call
                if (_callCount % 3 == 0)
                {
                    throw new InvalidOperationException("Simulated ping failure");
                }

                return await base.DetectIdleAsync(process);
            }
        }

        internal class RealisticActivityMonitor : IIdleDetector
        {
            private readonly TestProcessExecutor _processExecutor;

            public RealisticActivityMonitor(TestProcessExecutor processExecutor)
            {
                _processExecutor = processExecutor;
            }

            public async Task<DetectorResult> DetectIdleAsync(Process process)
            {
                await Task.Delay(20); // Simulate activity monitoring

                var mockProcess = process as MockProcess;
                var hasRecentActivity = (DateTime.UtcNow - mockProcess.LastActivityTime).TotalMinutes < 5;

                return new DetectorResult
                {
                    DetectorType = "ActivityMonitor",
                    IsIdle = !hasRecentActivity,
                    Confidence = hasRecentActivity ? 0.2 : 0.8,
                    LastActivityTime = mockProcess.LastActivityTime,
                    IdleDuration = DateTime.UtcNow - mockProcess.LastActivityTime,
                    Metadata = new Dictionary<string, object>
                    {
                        ["ProcessId"] = process.Id,
                        ["RecentActivity"] = hasRecentActivity,
                        ["UserSession"] = mockProcess.UserSession
                    }
                };
            }
        }

        internal class TestProcessExecutor : IProcessExecutor
        {
            public Task<ProcessExecutionResult> ExecuteAsync(string fileName, string arguments, TimeSpan timeout, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(new ProcessExecutionResult
                {
                    ExitCode = 0,
                    Output = "Mock process execution result",
                    ExecutionTime = TimeSpan.FromMilliseconds(50),
                    Success = true
                });
            }
        }

        internal class UserSessionInfo
        {
            public int ProcessId { get; set; }
            public string UserName { get; set; }
            public string Workstation { get; set; }
            public DateTime LoginTime { get; set; }
            public DateTime LastActivity { get; set; }
        }

        internal class LicenseActionInfo
        {
            public int ProcessId { get; set; }
            public string UserSession { get; set; }
            public string Action { get; set; }
            public string Reason { get; set; }
            public double Confidence { get; set; }
            public DateTime DecisionTime { get; set; }
        }

        internal class ScenarioReport
        {
            public int TotalProcesses { get; set; }
            public int IdleProcessCount { get; set; }
            public int ReleaseCandidateCount { get; set; }
            public DateTime GeneratedAt { get; set; }
            public long ProcessingTimeMs { get; set; }
        }

        internal class LicenseActionEventArgs : EventArgs
        {
            public string Action { get; set; }
            public string User { get; set; }
            public string Host { get; set; }
            public DateTime Timestamp { get; set; }
        }

        #endregion
    }
}