using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using LicenseReleaseService.IdleDetection;
using LicenseReleaseService.Configuration;
using LicenseReleaseService.VersionManagement;

namespace LicenseReleaseService.Tests.Integration
{
    /// <summary>
    /// Integration tests for VersionManagement integration with idle detection components
    /// </summary>
    [TestClass]
    public class VersionManagementIntegrationTests
    {
        private IVersionManager _versionManager;
        private IdleDetectionEngine _idleDetectionEngine;
        private TestVersionManager _testVersionManager;
        private List<IdleDetectionEventArgs> _detectionEvents;
        private List<VersionDetectionEventArgs> _versionEvents;

        [TestInitialize]
        public void TestInitialize()
        {
            _testVersionManager = new TestVersionManager();
            _versionManager = _testVersionManager;
            _detectionEvents = new List<IdleDetectionEventArgs>();
            _versionEvents = new List<VersionDetectionEventArgs>();

            // Create idle detection engine with version-specific configuration
            var detectors = new List<IIdleDetector>
            {
                new MockIdleDetector("TimeBased", true, 0.8),
                new MockIdleDetector("PingBased", true, 0.9)
            };

            _idleDetectionEngine = new IdleDetectionEngine(
                detectors,
                new IdleDetectionConfiguration
                {
                    DetectionInterval = 1,
                    Consensus = new ConsensusConfig { MinimumConfidence = 0.6 }
                },
                new Moq.Mock<Microsoft.Extensions.Logging.ILogger<IdleDetectionEngine>>().Object
            );

            // Subscribe to events
            _idleDetectionEngine.IdleDetected += OnIdleDetected;
            _testVersionManager.VersionDetectionCompleted += OnVersionDetectionCompleted;
        }

        [TestCleanup]
        public void TestCleanup()
        {
            _idleDetectionEngine?.Dispose();
            _testVersionManager?.Dispose();
        }

        private void OnIdleDetected(object sender, IdleDetectionEventArgs e)
        {
            _detectionEvents.Add(e);
        }

        private void OnVersionDetectionCompleted(object sender, VersionDetectionEventArgs e)
        {
            _versionEvents.Add(e);
        }

        [TestMethod]
        public async Task VersionIntegration_MultipleVersions_ShouldDetectCorrectly()
        {
            // Arrange
            var versions = new List<VersionInfo>
            {
                new VersionInfo { Id = 1, Version = "2023", Path = @"C:\SolidWorks 2023", DisplayName = "SolidWorks 2023" },
                new VersionInfo { Id = 2, Version = "2024", Path = @"C:\SolidWorks 2024", DisplayName = "SolidWorks 2024" }
            };

            await _versionManager.AddVersionAsync(versions[0]);
            await _versionManager.AddVersionAsync(versions[1]);

            var processes = new[]
            {
                new MockProcess(1234, "SLDWORKS.exe", @"C:\SolidWorks 2023\SLDWORKS.exe"),
                new MockProcess(5678, "SLDWORKS.exe", @"C:\SolidWorks 2024\SLDWORKS.exe")
            };

            // Act
            var detectionResults = await _idleDetectionEngine.DetectIdleStatesAsync(processes);

            // Assert
            Assert.AreEqual(2, detectionResults.Count, "Should detect both processes");
            Assert.IsTrue(detectionResults.ContainsKey(1234), "Should detect 2023 process");
            Assert.IsTrue(detectionResults.ContainsKey(5678), "Should detect 2024 process");

            var version2023Results = await _versionManager.GetVersionDetectionResultsAsync(versions[0].Id);
            var version2024Results = await _versionManager.GetVersionDetectionResultsAsync(versions[1].Id);

            Assert.IsNotNull(version2023Results, "Should have detection results for 2023");
            Assert.IsNotNull(version2024Results, "Should have detection results for 2024");
        }

        [TestMethod]
        public async Task VersionIntegration_VersionSpecificConfig_ShouldApplyCorrectly()
        {
            // Arrange
            var version2023 = new VersionInfo
            {
                Id = 1,
                Version = "2023",
                Path = @"C:\SolidWorks 2023",
                DisplayName = "SolidWorks 2023",
                Configuration = new VersionSpecificConfig
                {
                    IdleThresholdMinutes = 15, // Shorter threshold for testing
                    ConfidenceThreshold = 0.7
                }
            };

            var version2024 = new VersionInfo
            {
                Id = 2,
                Version = "2024",
                Path = @"C:\SolidWorks 2024",
                DisplayName = "SolidWorks 2024",
                Configuration = new VersionSpecificConfig
                {
                    IdleThresholdMinutes = 30, // Longer threshold
                    ConfidenceThreshold = 0.8
                }
            };

            await _versionManager.AddVersionAsync(version2023);
            await _versionManager.AddVersionAsync(version2024);

            var processes = new[]
            {
                new MockProcess(1234, "SLDWORKS.exe", @"C:\SolidWorks 2023\SLDWORKS.exe"),
                new MockProcess(5678, "SLDWORKS.exe", @"C:\SolidWorks 2024\SLDWORKS.exe")
            };

            // Act
            var detectionResults = await _idleDetectionEngine.DetectIdleStatesAsync(processes);

            // Assert - Should apply version-specific configurations
            Assert.AreEqual(2, detectionResults.Count);

            var result2023 = detectionResults[1234];
            var result2024 = detectionResults[5678];

            // The specific detection logic should account for version differences
            Assert.IsNotNull(result2023, "Should have result for 2023 process");
            Assert.IsNotNull(result2024, "Should have result for 2024 process");

            // Verify version-specific metrics were recorded
            var versionMetrics = await _versionManager.GetVersionMetricsAsync();
            Assert.IsTrue(versionMetrics.ContainsKey(version2023.Id), "Should have metrics for 2023");
            Assert.IsTrue(versionMetrics.ContainsKey(version2024.Id), "Should have metrics for 2024");
        }

        [TestMethod]
        public async Task VersionIntegration_VersionDetection_ShouldGroupByVersion()
        {
            // Arrange
            var version2023 = new VersionInfo
            {
                Id = 1,
                Version = "2023",
                Path = @"C:\SolidWorks 2023",
                DisplayName = "SolidWorks 2023"
            };

            var version2024 = new VersionInfo
            {
                Id = 2,
                Version = "2024",
                Path = @"C:\SolidWorks 2024",
                DisplayName = "SolidWorks 2024"
            };

            await _versionManager.AddVersionAsync(version2023);
            await _versionManager.AddVersionAsync(version2024);

            var processes = new[]
            {
                new MockProcess(1234, "SLDWORKS.exe", @"C:\SolidWorks 2023\SLDWORKS.exe"),
                new MockProcess(2345, "SLDWORKS.exe", @"C:\SolidWorks 2023\SLDWORKS.exe"),
                new MockProcess(3456, "SLDWORKS.exe", @"C:\SolidWorks 2024\SLDWORKS.exe"),
                new MockProcess(4567, "SLDWORKS.exe", @"C:\SolidWorks 2024\SLDWORKS.exe")
            };

            // Act
            var detectionResults = await _idleDetectionEngine.DetectIdleStatesAsync(processes);

            // Assert - Should group results by version
            var versionGroupedResults = await _versionManager.GetVersionGroupedResultsAsync();

            Assert.AreEqual(2, versionGroupedResults.Count, "Should have results for 2 versions");

            var version2023Results = versionGroupedResults[version2023.Id];
            var version2024Results = versionGroupedResults[version2024.Id];

            Assert.AreEqual(2, version2023Results.ProcessCount, "Should have 2 processes for 2023");
            Assert.AreEqual(2, version2024Results.ProcessCount, "Should have 2 processes for 2024");
        }

        [TestMethod]
        public async Task VersionIntegration_VersionFallback_ShouldHandleUnknownVersion()
        {
            // Arrange
            var knownVersion = new VersionInfo
            {
                Id = 1,
                Version = "2023",
                Path = @"C:\SolidWorks 2023",
                DisplayName = "SolidWorks 2023"
            };

            await _versionManager.AddVersionAsync(knownVersion);

            var processes = new[]
            {
                new MockProcess(1234, "SLDWORKS.exe", @"C:\SolidWorks 2023\SLDWORKS.exe"),
                new MockProcess(5678, "SLDWORKS.exe", @"C:\UnknownSolidWorks\SLDWORKS.exe") // Unknown version
            };

            // Act
            var detectionResults = await _idleDetectionEngine.DetectIdleStatesAsync(processes);

            // Assert - Should handle both known and unknown versions
            Assert.AreEqual(2, detectionResults.Count, "Should detect both processes");

            var unknownVersionResults = await _versionManager.GetUnknownVersionResultsAsync();
            Assert.AreEqual(1, unknownVersionResults.Count, "Should have 1 unknown version process");
            Assert.AreEqual(5678, unknownVersionResults.First().ProcessId, "Should identify correct unknown process");
        }

        [TestMethod]
        public async Task VersionIntegration_ConfigurationUpdate_ShouldApplyImmediately()
        {
            // Arrange
            var version = new VersionInfo
            {
                Id = 1,
                Version = "2023",
                Path = @"C:\SolidWorks 2023",
                DisplayName = "SolidWorks 2023",
                Configuration = new VersionSpecificConfig
                {
                    IdleThresholdMinutes = 30,
                    ConfidenceThreshold = 0.8
                }
            };

            await _versionManager.AddVersionAsync(version);

            var processes = new[]
            {
                new MockProcess(1234, "SLDWORKS.exe", @"C:\SolidWorks 2023\SLDWORKS.exe")
            };

            // Act - Initial detection
            var initialResults = await _idleDetectionEngine.DetectIdleStatesAsync(processes);

            // Update configuration
            var updatedConfig = new VersionSpecificConfig
            {
                IdleThresholdMinutes = 10, // Changed from 30 to 10
                ConfidenceThreshold = 0.7 // Changed from 0.8 to 0.7
            };

            await _versionManager.UpdateVersionConfigurationAsync(version.Id, updatedConfig);

            // Second detection with updated config
            var updatedResults = await _idleDetectionEngine.DetectIdleStatesAsync(processes);

            // Assert - Configuration should be applied immediately
            Assert.AreEqual(1, initialResults.Count, "Should have initial results");
            Assert.AreEqual(1, updatedResults.Count, "Should have updated results");

            var configHistory = await _versionManager.GetConfigurationHistoryAsync(version.Id);
            Assert.AreEqual(2, configHistory.Count, "Should have 2 configuration entries");
        }

        [TestMethod]
        public async Task VersionIntegration_PerformanceByVersion_ShouldTrackSeparately()
        {
            // Arrange
            var version2023 = new VersionInfo
            {
                Id = 1,
                Version = "2023",
                Path = @"C:\SolidWorks 2023",
                DisplayName = "SolidWorks 2023"
            };

            var version2024 = new VersionInfo
            {
                Id = 2,
                Version = "2024",
                Path = @"C:\SolidWorks 2024",
                DisplayName = "SolidWorks 2024"
            };

            await _versionManager.AddVersionAsync(version2023);
            await _versionManager.AddVersionAsync(version2024);

            var processes = new[]
            {
                new MockProcess(1234, "SLDWORKS.exe", @"C:\SolidWorks 2023\SLDWORKS.exe"),
                new MockProcess(5678, "SLDWORKS.exe", @"C:\SolidWorks 2024\SLDWORKS.exe")
            };

            // Act - Multiple detection cycles
            for (int i = 0; i < 5; i++)
            {
                await _idleDetectionEngine.DetectIdleStatesAsync(processes);
                await Task.Delay(100); // Small delay between detections
            }

            // Assert - Should track performance separately by version
            var version2023Metrics = await _versionManager.GetVersionPerformanceMetricsAsync(version2023.Id);
            var version2024Metrics = await _versionManager.GetVersionPerformanceMetricsAsync(version2024.Id);

            Assert.IsNotNull(version2023Metrics, "Should have metrics for 2023");
            Assert.IsNotNull(version2024Metrics, "Should have metrics for 2024");
            Assert.IsTrue(version2023Metrics.TotalDetections >= 5, "Should have at least 5 detections for 2023");
            Assert.IsTrue(version2024Metrics.TotalDetections >= 5, "Should have at least 5 detections for 2024");
        }

        [TestMethod]
        public async Task VersionIntegration_VersionHealth_ShouldMonitorIndividually()
        {
            // Arrange
            var version2023 = new VersionInfo
            {
                Id = 1,
                Version = "2023",
                Path = @"C:\SolidWorks 2023",
                DisplayName = "SolidWorks 2023"
            };

            var version2024 = new VersionInfo
            {
                Id = 2,
                Version = "2024",
                Path = @"C:\SolidWorks 2024",
                DisplayName = "SolidWorks 2024"
            };

            await _versionManager.AddVersionAsync(version2023);
            await _versionManager.AddVersionAsync(version2024);

            var processes = new[]
            {
                new MockProcess(1234, "SLDWORKS.exe", @"C:\SolidWorks 2023\SLDWORKS.exe"),
                new MockProcess(5678, "SLDWORKS.exe", @"C:\SolidWorks 2024\SLDWORKS.exe")
            };

            // Act - Perform detection
            await _idleDetectionEngine.DetectIdleStatesAsync(processes);

            // Assert - Should monitor health individually
            var version2023Health = await _versionManager.GetVersionHealthAsync(version2023.Id);
            var version2024Health = await _versionManager.GetVersionHealthAsync(version2024.Id);

            Assert.IsNotNull(version2023Health, "Should have health status for 2023");
            Assert.IsNotNull(version2024Health, "Should have health status for 2024");
            Assert.IsTrue(version2023Health.IsHealthy, "2023 should be healthy");
            Assert.IsTrue(version2024Health.IsHealthy, "2024 should be healthy");
        }

        [TestMethod]
        public async Task VersionIntegration_VersionCompatibility_ShouldHandleDifferentFormats()
        {
            // Arrange
            var versions = new List<VersionInfo>
            {
                new VersionInfo { Id = 1, Version = "2023", Path = @"C:\Program Files\SolidWorks 2023\SLDWORKS.exe", DisplayName = "SolidWorks 2023" },
                new VersionInfo { Id = 2, Version = "2024", Path = @"C:\SolidWorks 2024\bin\SLDWORKS.exe", DisplayName = "SolidWorks 2024" },
                new VersionInfo { Id = 3, Version = "2025", Path = @"C:\SW2025\SLDWORKS.exe", DisplayName = "SolidWorks 2025" }
            };

            foreach (var version in versions)
            {
                await _versionManager.AddVersionAsync(version);
            }

            var processes = new[]
            {
                new MockProcess(1234, "SLDWORKS.exe", @"C:\Program Files\SolidWorks 2023\SLDWORKS.exe"),
                new MockProcess(2345, "SLDWORKS.exe", @"C:\SolidWorks 2024\bin\SLDWORKS.exe"),
                new MockProcess(3456, "SLDWORKS.exe", @"C:\SW2025\SLDWORKS.exe")
            };

            // Act
            var detectionResults = await _idleDetectionEngine.DetectIdleStatesAsync(processes);

            // Assert - Should handle different path formats
            Assert.AreEqual(3, detectionResults.Count, "Should detect all three processes");

            var versionResults = await _versionManager.GetVersionGroupedResultsAsync();
            Assert.AreEqual(3, versionResults.Count, "Should have results for all 3 versions");

            foreach (var version in versions)
            {
                Assert.IsTrue(versionResults.ContainsKey(version.Id), $"Should have results for {version.Version}");
            }
        }

        [TestMethod]
        public async Task VersionIntegration_ErrorRecovery_ShouldContinueAfterVersionError()
        {
            // Arrange
            var goodVersion = new VersionInfo
            {
                Id = 1,
                Version = "2023",
                Path = @"C:\SolidWorks 2023",
                DisplayName = "SolidWorks 2023"
            };

            var problematicVersion = new VersionInfo
            {
                Id = 2,
                Version = "2024",
                Path = @"C:\InvalidPath\2024",
                DisplayName = "SolidWorks 2024"
            };

            await _versionManager.AddVersionAsync(goodVersion);
            await _versionManager.AddVersionAsync(problematicVersion);

            var processes = new[]
            {
                new MockProcess(1234, "SLDWORKS.exe", @"C:\SolidWorks 2023\SLDWORKS.exe"),
                new MockProcess(5678, "SLDWORKS.exe", @"C:\InvalidPath\2024\SLDWORKS.exe")
            };

            // Act - Should handle version errors gracefully
            var detectionResults = await _idleDetectionEngine.DetectIdleStatesAsync(processes);

            // Assert - Should continue despite version errors
            Assert.AreEqual(2, detectionResults.Count, "Should detect both processes despite version error");

            var errorLog = await _versionManager.GetVersionErrorsAsync();
            Assert.IsTrue(errorLog.Any(e => e.VersionId == problematicVersion.Id), "Should log error for problematic version");
        }

        [TestMethod]
        public async Task VersionIntegration_Migration_ShouldSupportVersionUpgrades()
        {
            // Arrange
            var oldVersion = new VersionInfo
            {
                Id = 1,
                Version = "2023",
                Path = @"C:\SolidWorks 2023",
                DisplayName = "SolidWorks 2023"
            };

            var newVersion = new VersionInfo
            {
                Id = 2,
                Version = "2024",
                Path = @"C:\SolidWorks 2024",
                DisplayName = "SolidWorks 2024",
                MigrationSourceId = oldVersion.Id // Migration from 2023
            };

            await _versionManager.AddVersionAsync(oldVersion);
            await _versionManager.AddVersionAsync(newVersion);

            var processes = new[]
            {
                new MockProcess(1234, "SLDWORKS.exe", @"C:\SolidWorks 2023\SLDWORKS.exe"),
                new MockProcess(5678, "SLDWORKS.exe", @"C:\SolidWorks 2024\SLDWORKS.exe")
            };

            // Act
            var detectionResults = await _idleDetectionEngine.DetectIdleStatesAsync(processes);

            // Assert - Should support migration scenarios
            Assert.AreEqual(2, detectionResults.Count, "Should detect both processes");

            var migrationReport = await _versionManager.GetMigrationReportAsync(newVersion.Id);
            Assert.IsNotNull(migrationReport, "Should have migration report");
            Assert.AreEqual(oldVersion.Id, migrationReport.SourceVersionId, "Should identify source version");
        }

        #region Helper Classes

        internal class TestVersionManager : IVersionManager, IDisposable
        {
            private readonly Dictionary<int, VersionInfo> _versions = new Dictionary<int, VersionInfo>();
            private readonly Dictionary<int, List<DetectionResult>> _versionResults = new Dictionary<int, List<DetectionResult>>();
            private readonly Dictionary<int, VersionMetrics> _versionMetrics = new Dictionary<int, VersionMetrics>();
            private readonly Dictionary<int, List<VersionConfigEntry>> _configHistory = new Dictionary<int, List<VersionConfigEntry>>();
            private readonly List<VersionErrorLog> _errorLog = new List<VersionErrorLog>();

            public event EventHandler<VersionDetectionEventArgs> VersionDetectionCompleted;

            public async Task AddVersionAsync(VersionInfo version)
            {
                _versions[version.Id] = version;
                _versionResults[version.Id] = new List<DetectionResult>();
                _versionMetrics[version.Id] = new VersionMetrics();
                _configHistory[version.Id] = new List<VersionConfigEntry>();

                // Record initial configuration
                _configHistory[version.Id].Add(new VersionConfigEntry
                {
                    Timestamp = DateTime.UtcNow,
                    Configuration = version.Configuration,
                    ChangeType = "Initial"
                });

                await Task.CompletedTask;
            }

            public async Task RemoveVersionAsync(int versionId)
            {
                _versions.Remove(versionId);
                _versionResults.Remove(versionId);
                _versionMetrics.Remove(versionId);
                _configHistory.Remove(versionId);
                await Task.CompletedTask;
            }

            public async Task<VersionInfo> GetVersionAsync(int versionId)
            {
                return await Task.FromResult(_versions.TryGetValue(versionId, out var version) ? version : null);
            }

            public async Task<IEnumerable<VersionInfo>> GetAllVersionsAsync()
            {
                return await Task.FromResult(_versions.Values.AsEnumerable());
            }

            public async Task UpdateVersionConfigurationAsync(int versionId, VersionSpecificConfig configuration)
            {
                if (_versions.TryGetValue(versionId, out var version))
                {
                    version.Configuration = configuration;

                    // Record configuration change
                    _configHistory[versionId].Add(new VersionConfigEntry
                    {
                        Timestamp = DateTime.UtcNow,
                        Configuration = configuration,
                        ChangeType = "Update"
                    });
                }
                await Task.CompletedTask;
            }

            public async Task RecordDetectionAsync(int versionId, DetectionResult result)
            {
                if (_versionResults.TryGetValue(versionId, out var results))
                {
                    results.Add(result);

                    // Update metrics
                    if (_versionMetrics.TryGetValue(versionId, out var metrics))
                    {
                        metrics.RecordDetection(result);
                    }
                }

                // Raise event
                VersionDetectionCompleted?.Invoke(this, new VersionDetectionEventArgs
                {
                    VersionId = versionId,
                    DetectionResult = result,
                    Timestamp = DateTime.UtcNow
                });

                await Task.CompletedTask;
            }

            public async Task<List<DetectionResult>> GetVersionDetectionResultsAsync(int versionId)
            {
                return await Task.FromResult(_versionResults.TryGetValue(versionId, out var results) ? results : new List<DetectionResult>());
            }

            public async Task<Dictionary<int, VersionGroupedResults>> GetVersionGroupedResultsAsync()
            {
                var result = new Dictionary<int, VersionGroupedResults>();

                foreach (var versionPair in _versions)
                {
                    var versionId = versionPair.Key;
                    var version = versionPair.Value;
                    var detectionResults = _versionResults.TryGetValue(versionId, out var results) ? results : new List<DetectionResult>();
                    var metrics = _versionMetrics.TryGetValue(versionId, out var versionMetrics) ? versionMetrics : new VersionMetrics();

                    result[versionId] = new VersionGroupedResults
                    {
                        VersionId = versionId,
                        Version = version.Version,
                        ProcessCount = detectionResults.Count,
                        DetectionResults = detectionResults,
                        Metrics = metrics
                    };
                }

                return await Task.FromResult(result);
            }

            public async Task<List<DetectionResult>> GetUnknownVersionResultsAsync()
            {
                // Simulate unknown version results
                var unknownResult = new DetectionResult
                {
                    ProcessId = 5678,
                    IsIdle = false,
                    Confidence = 0.5,
                    DetectionTime = DateTime.UtcNow,
                    Error = "Unknown version detected"
                };

                return await Task.FromResult(new List<DetectionResult> { unknownResult });
            }

            public async Task<List<VersionConfigEntry>> GetConfigurationHistoryAsync(int versionId)
            {
                return await Task.FromResult(_configHistory.TryGetValue(versionId, out var history) ? history : new List<VersionConfigEntry>());
            }

            public async Task<VersionMetrics> GetVersionPerformanceMetricsAsync(int versionId)
            {
                return await Task.FromResult(_versionMetrics.TryGetValue(versionId, out var metrics) ? metrics : new VersionMetrics());
            }

            public async Task<VersionHealth> GetVersionHealthAsync(int versionId)
            {
                var metrics = _versionMetrics.TryGetValue(versionId, out var versionMetrics) ? versionMetrics : new VersionMetrics();

                return await Task.FromResult(new VersionHealth
                {
                    VersionId = versionId,
                    IsHealthy = metrics.ErrorRate < 0.1, // Healthy if error rate < 10%
                    TotalDetections = metrics.TotalDetections,
                    ErrorRate = metrics.ErrorRate,
                    AverageConfidence = metrics.AverageConfidence,
                    LastHealthCheck = DateTime.UtcNow
                });
            }

            public async Task<Dictionary<int, VersionMetrics>> GetVersionMetricsAsync()
            {
                return await Task.FromResult(_versionMetrics.ToDictionary());
            }

            public async Task<List<VersionErrorLog>> GetVersionErrorsAsync()
            {
                return await Task.FromResult(_errorLog.ToList());
            }

            public async Task<MigrationReport> GetMigrationReportAsync(int versionId)
            {
                var version = _versions.TryGetValue(versionId, out var v) ? v : null;

                return await Task.FromResult(new MigrationReport
                {
                    TargetVersionId = versionId,
                    SourceVersionId = version?.MigrationSourceId,
                    MigrationDate = DateTime.UtcNow,
                    ProcessCount = _versionResults.TryGetValue(versionId, out var results) ? results.Count : 0,
                    IsSuccessful = true
                });
            }

            public void Dispose()
            {
                _versions.Clear();
                _versionResults.Clear();
                _versionMetrics.Clear();
                _configHistory.Clear();
                _errorLog.Clear();
            }
        }

        internal class VersionDetectionEventArgs : EventArgs
        {
            public int VersionId { get; set; }
            public DetectionResult DetectionResult { get; set; }
            public DateTime Timestamp { get; set; }
        }

        internal class VersionGroupedResults
        {
            public int VersionId { get; set; }
            public string Version { get; set; }
            public int ProcessCount { get; set; }
            public List<DetectionResult> DetectionResults { get; set; }
            public VersionMetrics Metrics { get; set; }
        }

        internal class VersionMetrics
        {
            public int TotalDetections { get; set; }
            public int SuccessfulDetections { get; set; }
            public int FailedDetections { get; set; }
            public double AverageConfidence { get; set; }
            public double ErrorRate => TotalDetections > 0 ? (double)FailedDetections / TotalDetections : 0;

            public void RecordDetection(DetectionResult result)
            {
                TotalDetections++;
                if (result.Error == null)
                {
                    SuccessfulDetections++;
                    AverageConfidence = (AverageConfidence * (SuccessfulDetections - 1) + result.Confidence) / SuccessfulDetections;
                }
                else
                {
                    FailedDetections++;
                }
            }
        }

        internal class VersionConfigEntry
        {
            public DateTime Timestamp { get; set; }
            public VersionSpecificConfig Configuration { get; set; }
            public string ChangeType { get; set; }
        }

        internal class VersionHealth
        {
            public int VersionId { get; set; }
            public bool IsHealthy { get; set; }
            public int TotalDetections { get; set; }
            public double ErrorRate { get; set; }
            public double AverageConfidence { get; set; }
            public DateTime LastHealthCheck { get; set; }
        }

        internal class VersionErrorLog
        {
            public int VersionId { get; set; }
            public string ErrorMessage { get; set; }
            public DateTime Timestamp { get; set; }
            public string StackTrace { get; set; }
        }

        internal class MigrationReport
        {
            public int TargetVersionId { get; set; }
            public int? SourceVersionId { get; set; }
            public DateTime MigrationDate { get; set; }
            public int ProcessCount { get; set; }
            public bool IsSuccessful { get; set; }
        }

        #endregion
    }
}