using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LicenseReleaseService.IdleDetection;
using LicenseReleaseService.Configuration;
using LicenseReleaseService.LicenseManagement;
using LicenseReleaseService.LicenseManagement.Caching;
using LicenseReleaseService.LicenseManagement.Models;
using LicenseReleaseService.LicenseManagement.Parsing;
using LicenseReleaseService.Process;
using LicenseReleaseService.TimerExecution;
using LicenseReleaseService.VersionManagement;

namespace LicenseReleaseService.Tests.Common
{
    /// <summary>
    /// Comprehensive mock framework for idle detection strategies and related components
    /// </summary>
    public static class DetectionStrategyMocks
    {
        #region Detector Mocks

        /// <summary>
        /// Creates a configurable mock detector for testing scenarios
        /// </summary>
        public class ConfigurableMockDetector : IIdleDetector
        {
            private readonly string _detectorType;
            private readonly Func<Process, Task<DetectorResult>> _detectionLogic;
            private readonly bool _shouldThrow;
            private readonly Exception _exceptionToThrow;

            public ConfigurableMockDetector(
                string detectorType,
                Func<Process, Task<DetectorResult>> detectionLogic,
                bool shouldThrow = false,
                Exception exceptionToThrow = null)
            {
                _detectorType = detectorType;
                _detectionLogic = detectionLogic;
                _shouldThrow = shouldThrow;
                _exceptionToThrow = exceptionToThrow;
            }

            public async Task<DetectorResult> DetectIdleAsync(Process process)
            {
                if (_shouldThrow)
                {
                    throw _exceptionToThrow ?? new InvalidOperationException($"Mock detector {_detectorType} failed");
                }

                return await _detectionLogic(process);
            }
        }

        /// <summary>
        /// Creates a detector that always returns the same result
        /// </summary>
        public class ConstantResultDetector : IIdleDetector
        {
            private readonly string _detectorType;
            private readonly DetectorResult _constantResult;

            public ConstantResultDetector(string detectorType, bool isIdle, double confidence)
            {
                _detectorType = detectorType;
                _constantResult = new DetectorResult
                {
                    DetectorType = _detectorType,
                    IsIdle = isIdle,
                    Confidence = confidence,
                    LastActivityTime = DateTime.UtcNow,
                    IdleDuration = TimeSpan.FromMinutes(30),
                    Metadata = new Dictionary<string, object>
                    {
                        ["Mock"] = true,
                        ["Constant"] = true
                    }
                };
            }

            public Task<DetectorResult> DetectIdleAsync(Process process)
            {
                return Task.FromResult(_constantResult);
            }
        }

        /// <summary>
        /// Creates a detector that returns results based on a sequence
        /// </summary>
        public class SequentialResultDetector : IIdleDetector
        {
            private readonly string _detectorType;
            private readonly Queue<DetectorResult> _results;
            private readonly bool _repeatSequence;

            public SequentialResultDetector(string detectorType, IEnumerable<DetectorResult> results, bool repeatSequence = false)
            {
                _detectorType = detectorType;
                _results = new Queue<DetectorResult>(results);
                _repeatSequence = repeatSequence;
            }

            public Task<DetectorResult> DetectIdleAsync(Process process)
            {
                if (_results.Count == 0)
                {
                    if (_repeatSequence)
                    {
                        throw new InvalidOperationException("Sequence exhausted and repeat is disabled");
                    }

                    // Return a default result when sequence is exhausted
                    return Task.FromResult(new DetectorResult
                    {
                        DetectorType = _detectorType,
                        IsIdle = false,
                        Confidence = 0.0,
                        Error = "Sequence exhausted"
                    });
                }

                var result = _results.Dequeue();
                if (_repeatSequence)
                {
                    _results.Enqueue(result);
                }

                return Task.FromResult(result);
            }
        }

        /// <summary>
        /// Creates a detector that simulates time-based detection
        /// </summary>
        public class TimeBasedMockDetector : IIdleDetector
        {
            private readonly TimeSpan _idleThreshold;
            private readonly double _maxConfidence;
            private readonly Func<Process, DateTime> _getLastActivityTime;

            public TimeBasedMockDetector(
                TimeSpan idleThreshold,
                double maxConfidence = 1.0,
                Func<Process, DateTime> getLastActivityTime = null)
            {
                _idleThreshold = idleThreshold;
                _maxConfidence = maxConfidence;
                _getLastActivityTime = getLastActivityTime ?? (p => DateTime.UtcNow.AddMinutes(-30));
            }

            public async Task<DetectorResult> DetectIdleAsync(Process process)
            {
                await Task.Delay(10); // Simulate processing time

                var lastActivity = _getLastActivityTime(process);
                var idleDuration = DateTime.UtcNow - lastActivity;
                var isIdle = idleDuration >= _idleThreshold;
                var confidence = Math.Min(idleDuration.TotalMinutes / _idleThreshold.TotalMinutes, _maxConfidence);

                return new DetectorResult
                {
                    DetectorType = "TimeBasedMock",
                    IsIdle = isIdle,
                    Confidence = confidence,
                    LastActivityTime = lastActivity,
                    IdleDuration = idleDuration,
                    Metadata = new Dictionary<string, object>
                    {
                        ["ThresholdMinutes"] = _idleThreshold.TotalMinutes,
                        ["IdleMinutes"] = idleDuration.TotalMinutes,
                        ["ProcessId"] = process.Id
                    }
                };
            }
        }

        /// <summary>
        /// Creates a detector that simulates ping-based detection
        /// </summary>
        public class PingBasedMockDetector : IIdleDetector
        {
            private readonly Func<Process, bool> _isResponsive;
            private readonly TimeSpan _pingDelay;
            private readonly double _idleConfidence;
            private readonly double _activeConfidence;

            public PingBasedMockDetector(
                Func<Process, bool> isResponsive = null,
                TimeSpan? pingDelay = null,
                double idleConfidence = 0.9,
                double activeConfidence = 0.1)
            {
                _isResponsive = isResponsive ?? (p => true);
                _pingDelay = pingDelay ?? TimeSpan.FromMilliseconds(50);
                _idleConfidence = idleConfidence;
                _activeConfidence = activeConfidence;
            }

            public async Task<DetectorResult> DetectIdleAsync(Process process)
            {
                await Task.Delay(_pingDelay);

                var isResponsive = _isResponsive(process);
                var lastActivity = DateTime.UtcNow.AddMinutes(isResponsive ? -5 : -30);

                return new DetectorResult
                {
                    DetectorType = "PingBasedMock",
                    IsIdle = !isResponsive,
                    Confidence = isResponsive ? _activeConfidence : _idleConfidence,
                    LastActivityTime = lastActivity,
                    IdleDuration = DateTime.UtcNow - lastActivity,
                    Metadata = new Dictionary<string, object>
                    {
                        ["PingResponseMs"] = _pingDelay.TotalMilliseconds,
                        ["Responding"] = isResponsive,
                        ["ProcessId"] = process.Id
                    }
                };
            }
        }

        #endregion

        #region Process Mocks

        /// <summary>
        /// Creates a mock process with configurable properties
        /// </summary>
        public class MockProcess : Process
        {
            private readonly int _id;
            private readonly string _processName;
            private readonly string _mainModulePath;
            private readonly DateTime _startTime;

            public DateTime LastActivityTime { get; set; }
            public bool IsResponding { get; set; }
            public string UserSession { get; set; }
            public Dictionary<string, object> ExtendedProperties { get; set; }

            public MockProcess(
                int id,
                string processName,
                string mainModulePath,
                DateTime? startTime = null)
            {
                _id = id;
                _processName = processName;
                _mainModulePath = mainModulePath;
                _startTime = startTime ?? DateTime.UtcNow.AddHours(-2);
                LastActivityTime = DateTime.UtcNow.AddMinutes(-10);
                IsResponding = true;
                ExtendedProperties = new Dictionary<string, object>();
            }

            public new int Id => _id;
            public new string ProcessName => _processName;
            public new DateTime StartTime => _startTime;
            public new bool Responding => IsResponding;

            public new ProcessModule MainModule => new MockProcessModule(_mainModulePath);

            public MockProcess WithActivity(DateTime lastActivityTime)
            {
                LastActivityTime = lastActivityTime;
                return this;
            }

            public MockProcess WithResponsiveness(bool isResponding)
            {
                IsResponding = isResponding;
                return this;
            }

            public MockProcess WithUserSession(string userSession)
            {
                UserSession = userSession;
                return this;
            }

            public MockProcess WithProperty(string key, object value)
            {
                ExtendedProperties[key] = value;
                return this;
            }
        }

        /// <summary>
        /// Mock process module for testing
        /// </summary>
        public class MockProcessModule : ProcessModule
        {
            private readonly string _fileName;

            public MockProcessModule(string fileName)
            {
                _fileName = fileName;
            }

            public new string FileName => _fileName;
        }

        /// <summary>
        /// Factory for creating test processes
        /// </summary>
        public static class MockProcessFactory
        {
            public static MockProcess CreateSolidWorksProcess(int id, string version = "2023")
            {
                var path = $@"C:\SolidWorks {version}\SLDWORKS.exe";
                return new MockProcess(id, "SLDWORKS.exe", path)
                    .WithUserSession($"user{id}@WORKSTATION{id}");
            }

            public static MockProcess[] CreateSolidWorksProcessRange(int startId, int count, string version = "2023")
            {
                var processes = new List<MockProcess>();
                for (int i = 0; i < count; i++)
                {
                    processes.Add(CreateSolidWorksProcess(startId + i, version));
                }
                return processes.ToArray();
            }

            public static MockProcess[] CreateMixedActivityProcesses(int count)
            {
                var processes = new List<MockProcess>();
                for (int i = 0; i < count; i++)
                {
                    var process = CreateSolidWorksProcess(1000 + i)
                        .WithActivity(DateTime.UtcNow.AddMinutes(-10 - (i * 5)))
                        .WithResponsiveness(i % 3 != 0);

                    processes.Add(process);
                }
                return processes.ToArray();
            }

            public static MockProcess[] CreateVersionMixedProcesses(int perVersionCount, params string[] versions)
            {
                var processes = new List<MockProcess>();
                foreach (var version in versions)
                {
                    var versionProcesses = CreateSolidWorksProcessRange(
                        1000 + processes.Count, perVersionCount, version);
                    processes.AddRange(versionProcesses);
                }
                return processes.ToArray();
            }
        }

        #endregion

        #region Service Mocks

        /// <summary>
        /// Mock cache manager for testing
        /// </summary>
        public class MockCacheManager : ICacheManager
        {
            private readonly Dictionary<string, object> _cache = new Dictionary<string, object>();
            private readonly Dictionary<string, DateTime> _expirationTimes = new Dictionary<string, DateTime>();
            private readonly HashSet<string> _invalidatedKeys = new HashSet<string>();

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
                return Get<Dictionary<string, LicenseInfo>>($"license_info_{server}_{port}");
            }

            public async Task<Dictionary<string, LicenseInfo>> GetLicenseInfoAsync(string server, int port, CancellationToken cancellationToken = default)
            {
                return await GetAsync<Dictionary<string, LicenseInfo>>($"license_info_{server}_{port}", cancellationToken);
            }

            public void SetLicenseInfo(string server, int port, Dictionary<string, LicenseInfo> licenseInfo, TimeSpan? expiration = null)
            {
                Set($"license_info_{server}_{port}", licenseInfo, expiration);
            }

            public async Task SetLicenseInfoAsync(string server, int port, Dictionary<string, LicenseInfo> licenseInfo, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
            {
                await SetAsync($"license_info_{server}_{port}", licenseInfo, expiration, cancellationToken);
            }

            public LicenseFeature GetLicenseFeature(string server, int port, string feature)
            {
                return Get<LicenseFeature>($"license_feature_{server}_{port}_{feature}");
            }

            public async Task<LicenseFeature> GetLicenseFeatureAsync(string server, int port, string feature, CancellationToken cancellationToken = default)
            {
                return await GetAsync<LicenseFeature>($"license_feature_{server}_{port}_{feature}", cancellationToken);
            }

            public void SetLicenseFeature(string server, int port, string feature, LicenseFeature licenseFeature, TimeSpan? expiration = null)
            {
                Set($"license_feature_{server}_{port}_{feature}", licenseFeature, expiration);
            }

            public async Task SetLicenseFeatureAsync(string server, int port, string feature, LicenseFeature licenseFeature, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
            {
                await SetAsync($"license_feature_{server}_{port}_{feature}", licenseFeature, expiration, cancellationToken);
            }

            public LicenseServerStatus GetServerStatus(string server, int port)
            {
                return Get<LicenseServerStatus>($"server_status_{server}_{port}");
            }

            public async Task<LicenseServerStatus> GetServerStatusAsync(string server, int port, CancellationToken cancellationToken = default)
            {
                return await GetAsync<LicenseServerStatus>($"server_status_{server}_{port}", cancellationToken);
            }

            public void SetServerStatus(string server, int port, LicenseServerStatus serverStatus, TimeSpan? expiration = null)
            {
                Set($"server_status_{server}_{port}", serverStatus, expiration);
            }

            public async Task SetServerStatusAsync(string server, int port, LicenseServerStatus serverStatus, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
            {
                await SetAsync($"server_status_{server}_{port}", serverStatus, expiration, cancellationToken);
            }

            public void Clear()
            {
                _cache.Clear();
                _expirationTimes.Clear();
                _invalidatedKeys.Clear();
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
                var key = $"{server}:{port}";
                _invalidatedKeys.Add(key);
            }

            public async Task InvalidateServerAsync(string server, int port, CancellationToken cancellationToken = default)
            {
                await Task.Run(() => InvalidateServer(server, port), cancellationToken);
            }

            public void InvalidateFeature(string server, int port, string feature)
            {
                var key = $"{server}:{port}:{feature}";
                _invalidatedKeys.Add(key);
            }

            public async Task InvalidateFeatureAsync(string server, int port, string feature, CancellationToken cancellationToken = default)
            {
                await Task.Run(() => InvalidateFeature(server, port, feature), cancellationToken);
            }

            public bool WasInvalidated(string key) => _invalidatedKeys.Contains(key);

            public void Reset()
            {
                Clear();
            }
        }

        /// <summary>
        /// Mock process executor for testing
        /// </summary>
        public class MockProcessExecutor : IProcessExecutor
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
        /// Mock timer execution service for testing
        /// </summary>
        public class MockTimerExecutionService : ITimerExecutionService, IDisposable
        {
            private readonly Dictionary<string, TimerTask> _tasks = new Dictionary<string, TimerTask>();
            private readonly Dictionary<string, int> _executionCounts = new Dictionary<string, int>();
            private readonly Dictionary<string, List<Func<CancellationToken, Task>>> _actions = new Dictionary<string, List<Func<CancellationToken, Task>>>();
            private readonly CancellationTokenSource _globalCts = new CancellationTokenSource();

            public async Task ScheduleTaskAsync(TimerTask task, Func<CancellationToken, Task> action, CancellationToken cancellationToken = default)
            {
                _tasks[task.Name] = task;
                _executionCounts[task.Name] = 0;

                if (!_actions.ContainsKey(task.Name))
                {
                    _actions[task.Name] = new List<Func<CancellationToken, Task>>();
                }
                _actions[task.Name].Add(action);

                // Simulate scheduled execution
                _ = Task.Run(async () =>
                {
                    while (!_globalCts.Token.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
                    {
                        await Task.Delay(task.Interval, _globalCts.Token);
                        if (!_globalCts.Token.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
                        {
                            await ExecuteTask(task.Name, _globalCts.Token);
                        }
                    }
                }, _globalCts.Token);
            }

            private async Task ExecuteTask(string taskName, CancellationToken cancellationToken)
            {
                if (_actions.TryGetValue(taskName, out var actions))
                {
                    foreach (var action in actions)
                    {
                        try
                        {
                            await action(cancellationToken);
                            _executionCounts[taskName]++;
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Task execution error for {taskName}: {ex.Message}");
                        }
                    }
                }
            }

            public async Task CancelTaskAsync(Guid taskId)
            {
                var task = _tasks.Values.FirstOrDefault(t => t.Id == taskId);
                if (task != null)
                {
                    _tasks.Remove(task.Name);
                    _actions.Remove(task.Name);
                }
                await Task.CompletedTask;
            }

            public async Task CancelAllTasksAsync()
            {
                _tasks.Clear();
                _actions.Clear();
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
                _globalCts.Cancel();
                _globalCts.Dispose();
                _tasks.Clear();
                _actions.Clear();
                _executionCounts.Clear();
            }
        }

        #endregion

        #region Builder Methods

        /// <summary>
        /// Builder for creating test detectors with common scenarios
        /// </summary>
        public static class DetectorBuilder
        {
            public static ConfigurableMockDetector CreateAlwaysIdle(string name = "AlwaysIdle", double confidence = 0.9)
            {
                return new ConfigurableMockDetector(name, process => Task.FromResult(new DetectorResult
                {
                    DetectorType = name,
                    IsIdle = true,
                    Confidence = confidence,
                    LastActivityTime = DateTime.UtcNow.AddMinutes(-30),
                    IdleDuration = TimeSpan.FromMinutes(30)
                }));
            }

            public static ConfigurableMockDetector CreateAlwaysActive(string name = "AlwaysActive", double confidence = 0.1)
            {
                return new ConfigurableMockDetector(name, process => Task.FromResult(new DetectorResult
                {
                    DetectorType = name,
                    IsIdle = false,
                    Confidence = confidence,
                    LastActivityTime = DateTime.UtcNow.AddMinutes(-1),
                    IdleDuration = TimeSpan.FromMinutes(1)
                }));
            }

            public static ConfigurableMockDetector CreateFailingDetector(string name = "FailingDetector", string errorMessage = "Simulated failure")
            {
                return new ConfigurableMockDetector(name, process => throw new InvalidOperationException(errorMessage));
            }

            public static ConfigurableMockDetector CreateDelayedDetector(string name = "DelayedDetector", TimeSpan delay, bool isIdle = true)
            {
                return new ConfigurableMockDetector(name, async process =>
                {
                    await Task.Delay(delay);
                    return new DetectorResult
                    {
                        DetectorType = name,
                        IsIdle = isIdle,
                        Confidence = 0.8,
                        LastActivityTime = DateTime.UtcNow.AddMinutes(-15),
                        IdleDuration = TimeSpan.FromMinutes(15)
                    };
                });
            }

            public static ConstantResultDetector CreateConstantResult(bool isIdle, double confidence, string name = "ConstantDetector")
            {
                return new ConstantResultDetector(name, isIdle, confidence);
            }

            public static SequentialResultDetector CreateSequentialResult(string name, params (bool isIdle, double confidence)[] results)
            {
                var detectorResults = results.Select(r => new DetectorResult
                {
                    DetectorType = name,
                    IsIdle = r.isIdle,
                    Confidence = r.confidence,
                    LastActivityTime = DateTime.UtcNow,
                    IdleDuration = TimeSpan.FromMinutes(30)
                }).ToList();

                return new SequentialResultDetector(name, detectorResults, repeatSequence: true);
            }
        }

        /// <summary>
        /// Builder for creating test scenarios
        /// </summary>
        public static class ScenarioBuilder
        {
            public static List<IIdleDetector> CreateMixedDetectors()
            {
                return new List<IIdleDetector>
                {
                    DetectorBuilder.CreateAlwaysIdle("TimeBased", 0.8),
                    DetectorBuilder.CreateAlwaysActive("PingBased", 0.2),
                    DetectorBuilder.CreateDelayedDetector("ActivityMonitor", TimeSpan.FromMilliseconds(50), false)
                };
            }

            public static List<IIdleDetector> CreateAllIdleDetectors()
            {
                return new List<IIdleDetector>
                {
                    DetectorBuilder.CreateAlwaysIdle("TimeBased", 0.9),
                    DetectorBuilder.CreateAlwaysIdle("PingBased", 0.85),
                    DetectorBuilder.CreateAlwaysIdle("ActivityMonitor", 0.8)
                };
            }

            public static List<IIdleDetector> CreateAllActiveDetectors()
            {
                return new List<IIdleDetector>
                {
                    DetectorBuilder.CreateAlwaysActive("TimeBased", 0.1),
                    DetectorBuilder.CreateAlwaysActive("PingBased", 0.15),
                    DetectorBuilder.CreateAlwaysActive("ActivityMonitor", 0.2)
                };
            }

            public static List<IIdleDetector> CreateFlakyDetectors()
            {
                return new List<IIdleDetector>
                {
                    DetectorBuilder.CreateAlwaysIdle("TimeBased", 0.8),
                    DetectorBuilder.CreateFailingDetector("PingBased"),
                    DetectorBuilder.CreateAlwaysActive("ActivityMonitor", 0.3)
                };
            }

            public static IdleDetectionConfiguration CreateStandardConfiguration()
            {
                return new IdleDetectionConfiguration
                {
                    DetectionInterval = 1,
                    Consensus = new ConsensusConfig
                    {
                        MinimumConfidence = 0.7,
                        RequireAllDetectors = false
                    }
                };
            }

            public static IdleDetectionConfiguration CreateStrictConfiguration()
            {
                return new IdleDetectionConfiguration
                {
                    DetectionInterval = 1,
                    Consensus = new ConsensusConfig
                    {
                        MinimumConfidence = 0.9,
                        RequireAllDetectors = true
                    }
                };
            }

            public static IdleDetectionConfiguration CreateLenientConfiguration()
            {
                return new IdleDetectionConfiguration
                {
                    DetectionInterval = 1,
                    Consensus = new ConsensusConfig
                    {
                        MinimumConfidence = 0.5,
                        RequireAllDetectors = false
                    }
                };
            }
        }

        #endregion
    }
}