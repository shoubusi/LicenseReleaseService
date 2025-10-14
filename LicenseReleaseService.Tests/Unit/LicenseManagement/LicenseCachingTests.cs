using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using LicenseReleaseService.LicenseManagement;
using LicenseReleaseService.LicenseManagement.Caching;
using LicenseReleaseService.LicenseManagement.Models;

namespace LicenseReleaseService.Tests.Unit.LicenseManagement
{
    [TestClass]
    public class LicenseCachingTests
    {
        private MemoryCacheManager _cacheManager;
        private CacheOptions _testOptions;

        [TestInitialize]
        public void Setup()
        {
            _testOptions = new CacheOptions
            {
                DefaultExpiration = TimeSpan.FromMinutes(5),
                LicenseInfoExpiration = TimeSpan.FromMinutes(10),
                LicenseFeatureExpiration = TimeSpan.FromMinutes(3),
                ServerStatusExpiration = TimeSpan.FromMinutes(2),
                KeyPrefix = "Test_",
                EnableBackgroundCleanup = false, // Disable for testing
                EnableStatistics = true,
                EnableDetailedLogging = false,
                EnableMemoryPressureMonitoring = false
            };
            _cacheManager = new MemoryCacheManager(_testOptions);
        }

        [TestCleanup]
        public void Cleanup()
        {
            _cacheManager?.Dispose();
        }

        #region Constructor Tests

        [TestMethod]
        public void Constructor_DefaultOptions_CreatesInstance()
        {
            // Act
            var cacheManager = new MemoryCacheManager();

            // Assert
            Assert.IsNotNull(cacheManager);
            Assert.IsNotNull(cacheManager.Options);
            Assert.IsNotNull(cacheManager.Statistics);
        }

        [TestMethod]
        public void Constructor_ValidOptions_CreatesInstance()
        {
            // Arrange
            var options = new CacheOptions();

            // Act
            var cacheManager = new MemoryCacheManager(options);

            // Assert
            Assert.IsNotNull(cacheManager);
            Assert.AreEqual(options, cacheManager.Options);
        }

        [TestMethod]
        public void Constructor_NullOptions_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.ThrowsException<ArgumentNullException>(() => new MemoryCacheManager(null));
        }

        [TestMethod]
        public void Constructor_InvalidOptions_ThrowsArgumentException()
        {
            // Arrange
            var invalidOptions = new CacheOptions
            {
                DefaultExpiration = TimeSpan.Zero // Invalid
            };

            // Act & Assert
            var exception = Assert.ThrowsException<ArgumentException>(() => new MemoryCacheManager(invalidOptions));
            Assert.IsTrue(exception.Message.Contains("Invalid cache options"));
        }

        #endregion

        #region Basic Cache Operations Tests

        [TestMethod]
        public void Get_NonExistentKey_ReturnsDefault()
        {
            // Arrange
            var key = "nonexistent_key";

            // Act
            var result = _cacheManager.Get<string>(key);

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        public void Get_ValidKey_ReturnsCachedValue()
        {
            // Arrange
            var key = "test_key";
            var value = "test_value";
            _cacheManager.Set(key, value);

            // Act
            var result = _cacheManager.Get<string>(key);

            // Assert
            Assert.AreEqual(value, result);
        }

        [TestMethod]
        public void Get_WrongType_ReturnsDefault()
        {
            // Arrange
            var key = "test_key";
            var value = "test_value";
            _cacheManager.Set(key, value);

            // Act
            var result = _cacheManager.Get<int>(key);

            // Assert
            Assert.AreEqual(default(int), result);
        }

        [TestMethod]
        public void Set_ValidValue_StoresInCache()
        {
            // Arrange
            var key = "test_key";
            var value = "test_value";

            // Act
            _cacheManager.Set(key, value);

            // Assert
            var result = _cacheManager.Get<string>(key);
            Assert.AreEqual(value, result);
        }

        [TestMethod]
        public void Set_WithExpiration_StoresWithCorrectExpiration()
        {
            // Arrange
            var key = "test_key";
            var value = "test_value";
            var expiration = TimeSpan.FromSeconds(1);

            // Act
            _cacheManager.Set(key, value, expiration);

            // Assert
            var result = _cacheManager.Get<string>(key);
            Assert.AreEqual(value, result);

            // Wait for expiration
            Thread.Sleep(1500);
            var expiredResult = _cacheManager.Get<string>(key);
            Assert.IsNull(expiredResult);
        }

        [TestMethod]
        public void Set_NullValue_ThrowsArgumentNullException()
        {
            // Arrange
            var key = "test_key";
            string value = null;

            // Act & Assert
            Assert.ThrowsException<ArgumentNullException>(() => _cacheManager.Set(key, value));
        }

        [TestMethod]
        public void Set_InvalidKey_ThrowsArgumentException()
        {
            // Arrange
            var key = "";
            var value = "test_value";

            // Act & Assert
            Assert.ThrowsException<ArgumentException>(() => _cacheManager.Set(key, value));
        }

        [TestMethod]
        public void Set_InvalidExpiration_ThrowsArgumentException()
        {
            // Arrange
            var key = "test_key";
            var value = "test_value";
            var expiration = TimeSpan.Zero;

            // Act & Assert
            Assert.ThrowsException<ArgumentException>(() => _cacheManager.Set(key, value, expiration));
        }

        [TestMethod]
        public void Remove_ExistentKey_RemovesAndReturnsTrue()
        {
            // Arrange
            var key = "test_key";
            var value = "test_value";
            _cacheManager.Set(key, value);

            // Act
            var result = _cacheManager.Remove(key);

            // Assert
            Assert.IsTrue(result);
            Assert.IsNull(_cacheManager.Get<string>(key));
        }

        [TestMethod]
        public void Remove_NonExistentKey_ReturnsFalse()
        {
            // Arrange
            var key = "nonexistent_key";

            // Act
            var result = _cacheManager.Remove(key);

            // Assert
            Assert.IsFalse(result);
        }

        [TestMethod]
        public void Contains_ExistentKey_ReturnsTrue()
        {
            // Arrange
            var key = "test_key";
            var value = "test_value";
            _cacheManager.Set(key, value);

            // Act
            var result = _cacheManager.Contains(key);

            // Assert
            Assert.IsTrue(result);
        }

        [TestMethod]
        public void Contains_NonExistentKey_ReturnsFalse()
        {
            // Arrange
            var key = "nonexistent_key";

            // Act
            var result = _cacheManager.Contains(key);

            // Assert
            Assert.IsFalse(result);
        }

        #endregion

        #region Async Operations Tests

        [TestMethod]
        public async Task GetAsync_ValidKey_ReturnsCachedValue()
        {
            // Arrange
            var key = "test_key";
            var value = "test_value";
            _cacheManager.Set(key, value);

            // Act
            var result = await _cacheManager.GetAsync<string>(key);

            // Assert
            Assert.AreEqual(value, result);
        }

        [TestMethod]
        public async Task SetAsync_ValidValue_StoresInCache()
        {
            // Arrange
            var key = "test_key";
            var value = "test_value";

            // Act
            await _cacheManager.SetAsync(key, value);

            // Assert
            var result = _cacheManager.Get<string>(key);
            Assert.AreEqual(value, result);
        }

        [TestMethod]
        public async Task RemoveAsync_ExistentKey_RemovesAndReturnsTrue()
        {
            // Arrange
            var key = "test_key";
            var value = "test_value";
            _cacheManager.Set(key, value);

            // Act
            var result = await _cacheManager.RemoveAsync(key);

            // Assert
            Assert.IsTrue(result);
            Assert.IsNull(_cacheManager.Get<string>(key));
        }

        [TestMethod]
        public async Task ContainsAsync_ExistentKey_ReturnsTrue()
        {
            // Arrange
            var key = "test_key";
            var value = "test_value";
            _cacheManager.Set(key, value);

            // Act
            var result = await _cacheManager.ContainsAsync(key);

            // Assert
            Assert.IsTrue(result);
        }

        [TestMethod]
        public async Task GetOrCreateAsync_KeyNotExists_CreatesAndReturnsValue()
        {
            // Arrange
            var key = "test_key";
            var expectedValue = "created_value";

            // Act
            var result = await _cacheManager.GetOrCreateAsync(key, async () => expectedValue);

            // Assert
            Assert.AreEqual(expectedValue, result);
            Assert.AreEqual(expectedValue, _cacheManager.Get<string>(key));
        }

        [TestMethod]
        public async Task GetOrCreateAsync_KeyExists_ReturnsCachedValue()
        {
            // Arrange
            var key = "test_key";
            var cachedValue = "cached_value";
            var factoryValue = "factory_value";
            _cacheManager.Set(key, cachedValue);

            // Act
            var result = await _cacheManager.GetOrCreateAsync(key, async () => factoryValue);

            // Assert
            Assert.AreEqual(cachedValue, result);
            Assert.AreEqual(cachedValue, _cacheManager.Get<string>(key));
        }

        #endregion

        #region License-Specific Cache Operations Tests

        [TestMethod]
        public void GetLicenseInfo_NonExistentServer_ReturnsNull()
        {
            // Arrange
            var server = "test-server";
            var port = 27000;

            // Act
            var result = _cacheManager.GetLicenseInfo(server, port);

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        public void SetLicenseInfo_ValidData_StoresAndRetrieves()
        {
            // Arrange
            var server = "test-server";
            var port = 27000;
            var licenseInfo = new Dictionary<string, LicenseInfo>
            {
                ["SolidWorks"] = new LicenseInfo { FeatureName = "SolidWorks", TotalLicenses = 10 }
            };

            // Act
            _cacheManager.SetLicenseInfo(server, port, licenseInfo);
            var result = _cacheManager.GetLicenseInfo(server, port);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.Count);
            Assert.AreEqual("SolidWorks", result["SolidWorks"].FeatureName);
            Assert.AreEqual(10, result["SolidWorks"].TotalLicenses);
        }

        [TestMethod]
        public async Task GetLicenseInfoAsync_ValidServer_ReturnsCachedInfo()
        {
            // Arrange
            var server = "test-server";
            var port = 27000;
            var licenseInfo = new Dictionary<string, LicenseInfo>
            {
                ["SolidWorks"] = new LicenseInfo { FeatureName = "SolidWorks", TotalLicenses = 10 }
            };
            await _cacheManager.SetLicenseInfoAsync(server, port, licenseInfo);

            // Act
            var result = await _cacheManager.GetLicenseInfoAsync(server, port);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.Count);
        }

        [TestMethod]
        public void GetLicenseFeature_NonExistentFeature_ReturnsNull()
        {
            // Arrange
            var server = "test-server";
            var port = 27000;
            var feature = "SolidWorks";

            // Act
            var result = _cacheManager.GetLicenseFeature(server, port, feature);

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        public void SetLicenseFeature_ValidData_StoresAndRetrieves()
        {
            // Arrange
            var server = "test-server";
            var port = 27000;
            var feature = "SolidWorks";
            var licenseFeature = new LicenseFeature
            {
                FeatureName = feature,
                TotalLicenses = 10,
                LicensesInUse = 5
            };

            // Act
            _cacheManager.SetLicenseFeature(server, port, feature, licenseFeature);
            var result = _cacheManager.GetLicenseFeature(server, port, feature);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual(feature, result.FeatureName);
            Assert.AreEqual(10, result.TotalLicenses);
            Assert.AreEqual(5, result.LicensesInUse);
        }

        [TestMethod]
        public void GetServerStatus_NonExistentServer_ReturnsNull()
        {
            // Arrange
            var server = "test-server";
            var port = 27000;

            // Act
            var result = _cacheManager.GetServerStatus(server, port);

            // Assert
            Assert.IsNull(result);
        }

        [TestMethod]
        public void SetServerStatus_ValidData_StoresAndRetrieves()
        {
            // Arrange
            var server = "test-server";
            var port = 27000;
            var serverStatus = new LicenseServerStatus
            {
                ServerAddress = $"{server}:{port}",
                IsServerUp = true,
                StatusMessage = "Server is UP"
            };

            // Act
            _cacheManager.SetServerStatus(server, port, serverStatus);
            var result = _cacheManager.GetServerStatus(server, port);

            // Assert
            Assert.IsNotNull(result);
            Assert.AreEqual($"{server}:{port}", result.ServerAddress);
            Assert.IsTrue(result.IsServerUp);
            Assert.AreEqual("Server is UP", result.StatusMessage);
        }

        #endregion

        #region Cache Key Generation Tests

        [TestMethod]
        public void GenerateLicenseInfoKey_ValidInput_ReturnsCorrectKey()
        {
            // Arrange
            var server = "test-server";
            var port = 27000;

            // Act
            var result = _cacheManager.GenerateLicenseInfoKey(server, port);

            // Assert
            Assert.AreEqual($"LicenseInfo_{server}_{port}", result);
        }

        [TestMethod]
        public void GenerateLicenseFeatureKey_ValidInput_ReturnsCorrectKey()
        {
            // Arrange
            var server = "test-server";
            var port = 27000;
            var feature = "SolidWorks";

            // Act
            var result = _cacheManager.GenerateLicenseFeatureKey(server, port, feature);

            // Assert
            Assert.AreEqual($"LicenseFeature_{server}_{port}_{feature}", result);
        }

        [TestMethod]
        public void GenerateServerStatusKey_ValidInput_ReturnsCorrectKey()
        {
            // Arrange
            var server = "test-server";
            var port = 27000;

            // Act
            var result = _cacheManager.GenerateServerStatusKey(server, port);

            // Assert
            Assert.AreEqual($"ServerStatus_{server}_{port}", result);
        }

        [TestMethod]
        public void CacheKeyGeneration_UniqueServers_GeneratesUniqueKeys()
        {
            // Arrange
            var server1 = "server1";
            var server2 = "server2";
            var port = 27000;

            // Act
            var key1 = _cacheManager.GenerateLicenseInfoKey(server1, port);
            var key2 = _cacheManager.GenerateLicenseInfoKey(server2, port);

            // Assert
            Assert.AreNotEqual(key1, key2);
        }

        [TestMethod]
        public void CacheKeyGeneration_UniquePorts_GeneratesUniqueKeys()
        {
            // Arrange
            var server = "test-server";
            var port1 = 27000;
            var port2 = 27001;

            // Act
            var key1 = _cacheManager.GenerateLicenseInfoKey(server, port1);
            var key2 = _cacheManager.GenerateLicenseInfoKey(server, port2);

            // Assert
            Assert.AreNotEqual(key1, key2);
        }

        [TestMethod]
        public void CacheKeyGeneration_UniqueFeatures_GeneratesUniqueKeys()
        {
            // Arrange
            var server = "test-server";
            var port = 27000;
            var feature1 = "SolidWorks";
            var feature2 = "SolidWorks_Explorer";

            // Act
            var key1 = _cacheManager.GenerateLicenseFeatureKey(server, port, feature1);
            var key2 = _cacheManager.GenerateLicenseFeatureKey(server, port, feature2);

            // Assert
            Assert.AreNotEqual(key1, key2);
        }

        #endregion

        #region Cache Invalidation Tests

        [TestMethod]
        public void InvalidateServer_ValidServer_RemovesAllRelatedEntries()
        {
            // Arrange
            var server = "test-server";
            var port = 27000;

            // Add various entries for the server
            _cacheManager.SetLicenseInfo(server, port, new Dictionary<string, LicenseInfo>());
            _cacheManager.SetLicenseFeature(server, port, "SolidWorks", new LicenseFeature());
            _cacheManager.SetServerStatus(server, port, new LicenseServerStatus());

            // Act
            _cacheManager.InvalidateServer(server, port);

            // Assert
            Assert.IsNull(_cacheManager.GetLicenseInfo(server, port));
            Assert.IsNull(_cacheManager.GetLicenseFeature(server, port, "SolidWorks"));
            Assert.IsNull(_cacheManager.GetServerStatus(server, port));
        }

        [TestMethod]
        public void InvalidateFeature_ValidServerAndFeature_RemovesFeatureEntries()
        {
            // Arrange
            var server = "test-server";
            var port = 27000;
            var feature1 = "SolidWorks";
            var feature2 = "SolidWorks_Explorer";

            _cacheManager.SetLicenseFeature(server, port, feature1, new LicenseFeature());
            _cacheManager.SetLicenseFeature(server, port, feature2, new LicenseFeature());
            _cacheManager.SetLicenseInfo(server, port, new Dictionary<string, LicenseInfo>());
            _cacheManager.SetServerStatus(server, port, new LicenseServerStatus());

            // Act
            _cacheManager.InvalidateFeature(server, port, feature1);

            // Assert
            Assert.IsNull(_cacheManager.GetLicenseFeature(server, port, feature1));
            Assert.IsNotNull(_cacheManager.GetLicenseFeature(server, port, feature2)); // Should remain
            Assert.IsNotNull(_cacheManager.GetLicenseInfo(server, port)); // Should remain
            Assert.IsNotNull(_cacheManager.GetServerStatus(server, port)); // Should remain
        }

        [TestMethod]
        public async Task InvalidateServerAsync_ValidServer_RemovesAllRelatedEntries()
        {
            // Arrange
            var server = "test-server";
            var port = 27000;

            _cacheManager.SetLicenseInfo(server, port, new Dictionary<string, LicenseInfo>());
            _cacheManager.SetLicenseFeature(server, port, "SolidWorks", new LicenseFeature());
            _cacheManager.SetServerStatus(server, port, new LicenseServerStatus());

            // Act
            await _cacheManager.InvalidateServerAsync(server, port);

            // Assert
            Assert.IsNull(_cacheManager.GetLicenseInfo(server, port));
            Assert.IsNull(_cacheManager.GetLicenseFeature(server, port, "SolidWorks"));
            Assert.IsNull(_cacheManager.GetServerStatus(server, port));
        }

        [TestMethod]
        public async Task InvalidateFeatureAsync_ValidServerAndFeature_RemovesFeatureEntries()
        {
            // Arrange
            var server = "test-server";
            var port = 27000;
            var feature = "SolidWorks";

            _cacheManager.SetLicenseFeature(server, port, feature, new LicenseFeature());

            // Act
            await _cacheManager.InvalidateFeatureAsync(server, port, feature);

            // Assert
            Assert.IsNull(_cacheManager.GetLicenseFeature(server, port, feature));
        }

        #endregion

        #region Cache Expiration and Cleanup Tests

        [TestMethod]
        public void RemoveExpired_ExpiredItems_RemovesExpiredItems()
        {
            // Arrange
            var key1 = "key1";
            var key2 = "key2";
            var key3 = "key3";

            _cacheManager.Set(key1, "value1", TimeSpan.FromMilliseconds(100));
            _cacheManager.Set(key2, "value2", TimeSpan.FromSeconds(5));
            _cacheManager.Set(key3, "value3", TimeSpan.FromMilliseconds(100));

            // Wait for some items to expire
            Thread.Sleep(200);

            // Act
            var removedCount = _cacheManager.RemoveExpired();

            // Assert
            Assert.IsTrue(removedCount >= 2); // At least key1 and key3 should be expired
            Assert.IsNull(_cacheManager.Get<string>(key1));
            Assert.IsNotNull(_cacheManager.Get<string>(key2)); // Should still exist
            Assert.IsNull(_cacheManager.Get<string>(key3));
        }

        [TestMethod]
        public async Task RemoveExpiredAsync_ExpiredItems_RemovesExpiredItems()
        {
            // Arrange
            var key1 = "key1";
            var key2 = "key2";

            _cacheManager.Set(key1, "value1", TimeSpan.FromMilliseconds(100));
            _cacheManager.Set(key2, "value2", TimeSpan.FromSeconds(5));

            // Wait for expiration
            Thread.Sleep(200);

            // Act
            var removedCount = await _cacheManager.RemoveExpiredAsync();

            // Assert
            Assert.IsTrue(removedCount >= 1);
            Assert.IsNull(_cacheManager.Get<string>(key1));
            Assert.IsNotNull(_cacheManager.Get<string>(key2));
        }

        [TestMethod]
        public void Clear_AllItems_RemovesAllItems()
        {
            // Arrange
            _cacheManager.Set("key1", "value1");
            _cacheManager.Set("key2", "value2");
            _cacheManager.Set("key3", "value3");

            // Act
            _cacheManager.Clear();

            // Assert
            Assert.IsNull(_cacheManager.Get<string>("key1"));
            Assert.IsNull(_cacheManager.Get<string>("key2"));
            Assert.IsNull(_cacheManager.Get<string>("key3"));
        }

        [TestMethod]
        public async Task ClearAsync_AllItems_RemovesAllItems()
        {
            // Arrange
            _cacheManager.Set("key1", "value1");
            _cacheManager.Set("key2", "value2");

            // Act
            await _cacheManager.ClearAsync();

            // Assert
            Assert.IsNull(_cacheManager.Get<string>("key1"));
            Assert.IsNull(_cacheManager.Get<string>("key2"));
        }

        #endregion

        #region Statistics Tests

        [TestMethod]
        public void Statistics_AfterOperations_RecordsCorrectStatistics()
        {
            // Arrange
            var key = "test_key";
            var value = "test_value";

            // Act
            _cacheManager.Set(key, value);
            var hit = _cacheManager.Get<string>(key);
            var miss = _cacheManager.Get<string>("nonexistent");
            _cacheManager.Remove(key);
            var missAfterRemove = _cacheManager.Get<string>(key);

            // Assert
            Assert.IsTrue(_cacheManager.Statistics.TotalRequests >= 3);
            Assert.IsTrue(_cacheManager.Statistics.CacheHits >= 1);
            Assert.IsTrue(_cacheManager.Statistics.CacheMisses >= 2);
            Assert.IsTrue(_cacheManager.Statistics.TotalAdditions >= 1);
            Assert.IsTrue(_cacheManager.Statistics.TotalRemovals >= 1);
        }

        [TestMethod]
        public void Statistics_GetOrCreateRecordsCorrectStatistics()
        {
            // Arrange
            var key = "test_key";
            var value = "test_value";

            // Act
            var result1 = _cacheManager.GetOrCreate(key, () => value);
            var result2 = _cacheManager.GetOrCreate(key, () => "different_value");

            // Assert
            Assert.AreEqual(value, result1);
            Assert.AreEqual(value, result2);
            Assert.IsTrue(_cacheManager.Statistics.TotalAdditions >= 1);
            Assert.IsTrue(_cacheManager.Statistics.CacheHits >= 1);
        }

        #endregion

        #region Performance Tests

        [TestMethod]
        public void Performance_SetAndGetMultipleOperations_CompletesQuickly()
        {
            // Arrange
            var operations = 1000;
            var keys = Enumerable.Range(0, operations).Select(i => $"key_{i}").ToList();
            var values = Enumerable.Range(0, operations).Select(i => $"value_{i}").ToList();

            // Act
            var startTime = DateTime.UtcNow;

            for (int i = 0; i < operations; i++)
            {
                _cacheManager.Set(keys[i], values[i]);
            }

            for (int i = 0; i < operations; i++)
            {
                var result = _cacheManager.Get<string>(keys[i]);
                Assert.AreEqual(values[i], result);
            }

            var duration = DateTime.UtcNow - startTime;

            // Assert
            Assert.IsTrue(duration.TotalMilliseconds < 5000, $"{operations} set/get operations should complete within 5 seconds, took {duration.TotalMilliseconds}ms");
        }

        [TestMethod]
        public void Performance_ConcurrentOperations_ThreadSafe()
        {
            // Arrange
            var tasks = new Task[10];
            var operationsPerTask = 100;

            // Act
            for (int i = 0; i < tasks.Length; i++)
            {
                var taskIndex = i;
                tasks[i] = Task.Run(() =>
                {
                    for (int j = 0; j < operationsPerTask; j++)
                    {
                        var key = $"task_{taskIndex}_key_{j}";
                        var value = $"task_{taskIndex}_value_{j}";

                        _cacheManager.Set(key, value);
                        var result = _cacheManager.Get<string>(key);
                        Assert.AreEqual(value, result);
                    }
                });
            }

            Task.WaitAll(tasks);

            // Assert
            var totalOperations = tasks.Length * operationsPerTask;
            Assert.IsTrue(_cacheManager.Statistics.TotalAdditions >= totalOperations);
            Assert.IsTrue(_cacheManager.Statistics.CacheHits >= totalOperations);
        }

        [TestMethod]
        public void Performance_LicenseSpecificOperations_CompletesQuickly()
        {
            // Arrange
            var servers = Enumerable.Range(0, 10).Select(i => $"server_{i}").ToList();
            var ports = Enumerable.Range(27000, 10).ToList();
            var features = new[] { "SolidWorks", "SolidWorks_Explorer", "SolidWorks_Electric", "SolidWorks_PDM" };

            // Act
            var startTime = DateTime.UtcNow;

            foreach (var server in servers)
            {
                foreach (var port in ports)
                {
                    // License info operations
                    var licenseInfo = new Dictionary<string, LicenseInfo>();
                    foreach (var feature in features)
                    {
                        licenseInfo[feature] = new LicenseInfo { FeatureName = feature, TotalLicenses = 10 };
                    }
                    _cacheManager.SetLicenseInfo(server, port, licenseInfo);
                    var retrievedLicenseInfo = _cacheManager.GetLicenseInfo(server, port);
                    Assert.IsNotNull(retrievedLicenseInfo);

                    // License feature operations
                    foreach (var feature in features)
                    {
                        var licenseFeature = new LicenseFeature { FeatureName = feature, TotalLicenses = 10 };
                        _cacheManager.SetLicenseFeature(server, port, feature, licenseFeature);
                        var retrievedFeature = _cacheManager.GetLicenseFeature(server, port, feature);
                        Assert.IsNotNull(retrievedFeature);
                    }

                    // Server status operations
                    var serverStatus = new LicenseServerStatus { ServerAddress = $"{server}:{port}", IsServerUp = true };
                    _cacheManager.SetServerStatus(server, port, serverStatus);
                    var retrievedStatus = _cacheManager.GetServerStatus(server, port);
                    Assert.IsNotNull(retrievedStatus);
                }
            }

            var duration = DateTime.UtcNow - startTime;

            // Assert
            Assert.IsTrue(duration.TotalMilliseconds < 3000, "License-specific operations should complete within 3 seconds");
        }

        [TestMethod]
        public void Performance_CacheInvalidateOperations_CompletesQuickly()
        {
            // Arrange
            var servers = Enumerable.Range(0, 50).Select(i => $"server_{i}").ToList();
            var ports = Enumerable.Range(27000, 50).ToList();
            var features = new[] { "SolidWorks", "SolidWorks_Explorer", "SolidWorks_Electric" };

            // Populate cache
            foreach (var server in servers)
            {
                foreach (var port in ports)
                {
                    _cacheManager.SetLicenseInfo(server, port, new Dictionary<string, LicenseInfo>());
                    foreach (var feature in features)
                    {
                        _cacheManager.SetLicenseFeature(server, port, feature, new LicenseFeature());
                    }
                    _cacheManager.SetServerStatus(server, port, new LicenseServerStatus());
                }
            }

            // Act
            var startTime = DateTime.UtcNow;

            foreach (var server in servers.Take(10))
            {
                foreach (var port in ports.Take(10))
                {
                    _cacheManager.InvalidateServer(server, port);
                    _cacheManager.InvalidateFeature(server, port, features[0]);
                }
            }

            var duration = DateTime.UtcNow - startTime;

            // Assert
            Assert.IsTrue(duration.TotalMilliseconds < 2000, "Cache invalidation operations should complete within 2 seconds");
        }

        #endregion

        #region Edge Cases Tests

        [TestMethod]
        public void Set_LargeObject_HandlesGracefully()
        {
            // Arrange
            var key = "large_key";
            var largeValue = new string('x', 1000000); // 1MB string

            // Act
            _cacheManager.Set(key, largeValue);
            var result = _cacheManager.Get<string>(key);

            // Assert
            Assert.AreEqual(largeValue, result);
        }

        [TestMethod]
        public void GetOrCreate_FactoryReturnsNull_DoesNotCache()
        {
            // Arrange
            var key = "null_key";

            // Act
            var result = _cacheManager.GetOrCreate<string>(key, () => null);

            // Assert
            Assert.IsNull(result);
            Assert.IsFalse(_cacheManager.Contains(key));
        }

        [TestMethod]
        public async Task GetOrCreateAsync_FactoryReturnsNull_DoesNotCache()
        {
            // Arrange
            var key = "null_key";

            // Act
            var result = await _cacheManager.GetOrCreateAsync<string>(key, async () => null);

            // Assert
            Assert.IsNull(result);
            Assert.IsFalse(_cacheManager.Contains(key));
        }

        [TestMethod]
        public void Operations_WithSpecialCharactersInKeys_WorksCorrectly()
        {
            // Arrange
            var keys = new[]
            {
                "key_with_underscores",
                "key-with-dashes",
                "key.with.dots",
                "key with spaces",
                "key@with#special$characters",
                "key/with/slashes",
                "key\\with\\backslashes"
            };

            // Act & Assert
            foreach (var key in keys)
            {
                var value = $"value_for_{key}";
                _cacheManager.Set(key, value);
                var result = _cacheManager.Get<string>(key);
                Assert.AreEqual(value, result);
            }
        }

        #endregion

        #region GetOrCreate Tests

        [TestMethod]
        public void GetOrCreate_KeyNotExists_CreatesAndReturnsValue()
        {
            // Arrange
            var key = "test_key";
            var expectedValue = "created_value";

            // Act
            var result = _cacheManager.GetOrCreate(key, () => expectedValue);

            // Assert
            Assert.AreEqual(expectedValue, result);
            Assert.AreEqual(expectedValue, _cacheManager.Get<string>(key));
        }

        [TestMethod]
        public void GetOrCreate_KeyExists_ReturnsCachedValue()
        {
            // Arrange
            var key = "test_key";
            var cachedValue = "cached_value";
            var factoryValue = "factory_value";
            _cacheManager.Set(key, cachedValue);

            // Act
            var result = _cacheManager.GetOrCreate(key, () => factoryValue);

            // Assert
            Assert.AreEqual(cachedValue, result);
            Assert.AreEqual(cachedValue, _cacheManager.Get<string>(key));
        }

        [TestMethod]
        public void GetOrCreate_WithCustomExpiration_SetsWithCorrectExpiration()
        {
            // Arrange
            var key = "test_key";
            var value = "test_value";
            var expiration = TimeSpan.FromSeconds(1);

            // Act
            var result = _cacheManager.GetOrCreate(key, () => value, expiration);

            // Assert
            Assert.AreEqual(value, result);
            Thread.Sleep(1500);
            var expiredResult = _cacheManager.Get<string>(key);
            Assert.IsNull(expiredResult);
        }

        [TestMethod]
        public void GetOrCreate_NullFactory_ThrowsArgumentNullException()
        {
            // Arrange
            var key = "test_key";

            // Act & Assert
            Assert.ThrowsException<ArgumentNullException>(() => _cacheManager.GetOrCreate<string>(key, null));
        }

        [TestMethod]
        public void GetOrCreate_EmptyKey_ThrowsArgumentException()
        {
            // Arrange
            var key = "";

            // Act & Assert
            Assert.ThrowsException<ArgumentException>(() => _cacheManager.GetOrCreate(key, () => "value"));
        }

        #endregion
    }
}