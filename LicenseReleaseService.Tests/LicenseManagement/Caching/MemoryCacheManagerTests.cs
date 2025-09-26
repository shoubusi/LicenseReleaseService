using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using LicenseReleaseService.LicenseManagement.Caching;
using LicenseReleaseService.LicenseManagement.Models;
using Xunit;

namespace LicenseReleaseService.Tests.LicenseManagement.Caching
{
    /// <summary>
    /// Unit tests for MemoryCacheManager class
    /// </summary>
    public class MemoryCacheManagerTests : IDisposable
    {
        private readonly MemoryCacheManager _cacheManager;
        private readonly CacheOptions _testOptions;

        public MemoryCacheManagerTests()
        {
            _testOptions = new CacheOptions
            {
                DefaultExpiration = TimeSpan.FromMinutes(1),
                MaximumItems = 100,
                EnableSlidingExpiration = true,
                EnableStatistics = true,
                EnableDetailedLogging = false,
                EnableBackgroundCleanup = false,
                EnableMemoryPressureMonitoring = false
            };

            _cacheManager = new MemoryCacheManager(_testOptions);
        }

        public void Dispose()
        {
            _cacheManager?.Dispose();
        }

        [Fact]
        public void Constructor_DefaultOptions_UsesDefaultValues()
        {
            var manager = new MemoryCacheManager();

            Assert.NotNull(manager.Options);
            Assert.NotNull(manager.Statistics);
            Assert.Equal(TimeSpan.FromMinutes(5), manager.Options.DefaultExpiration);
        }

        [Fact]
        public void Constructor_CustomOptions_UsesCustomValues()
        {
            var options = new CacheOptions
            {
                DefaultExpiration = TimeSpan.FromMinutes(30),
                MaximumItems = 5000,
                KeyPrefix = "Test_"
            };

            var manager = new MemoryCacheManager(options);

            Assert.Equal(options.DefaultExpiration, manager.Options.DefaultExpiration);
            Assert.Equal(options.MaximumItems, manager.Options.MaximumItems);
            Assert.Equal(options.KeyPrefix, manager.Options.KeyPrefix);
        }

        [Fact]
        public void Constructor_InvalidOptions_ThrowsArgumentException()
        {
            var invalidOptions = new CacheOptions
            {
                DefaultExpiration = TimeSpan.Zero
            };

            Assert.Throws<ArgumentException>(() => new MemoryCacheManager(invalidOptions));
        }

        [Fact]
        public void Get_KeyNotExists_ReturnsDefault()
        {
            var result = _cacheManager.Get<string>("nonexistentKey");

            Assert.Null(result);
        }

        [Fact]
        public void Get_KeyExists_ReturnsCorrectValue()
        {
            var key = "testKey";
            var value = "testValue";

            _cacheManager.Set(key, value);
            var result = _cacheManager.Get<string>(key);

            Assert.Equal(value, result);
        }

        [Fact]
        public void Get_WithDifferentTypes_ReturnsCorrectValues()
        {
            var stringKey = "stringKey";
            var stringValue = "testString";
            var intKey = "intKey";
            var intValue = 42;

            _cacheManager.Set(stringKey, stringValue);
            _cacheManager.Set(intKey, intValue);

            var stringResult = _cacheManager.Get<string>(stringKey);
            var intResult = _cacheManager.Get<int>(intKey);

            Assert.Equal(stringValue, stringResult);
            Assert.Equal(intValue, intResult);
        }

        [Fact]
        public void Get_InvalidKey_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => _cacheManager.Get<string>(null));
            Assert.Throws<ArgumentException>(() => _cacheManager.Get<string>(""));
            Assert.Throws<ArgumentException>(() => _cacheManager.Get<string>("   "));
        }

        [Fact]
        public async Task GetAsync_KeyExists_ReturnsCorrectValue()
        {
            var key = "testKey";
            var value = "testValue";

            await _cacheManager.SetAsync(key, value);
            var result = await _cacheManager.GetAsync<string>(key);

            Assert.Equal(value, result);
        }

        [Fact]
        public void Set_WithDefaultExpiration_SetsValue()
        {
            var key = "testKey";
            var value = "testValue";

            _cacheManager.Set(key, value);
            var result = _cacheManager.Get<string>(key);

            Assert.Equal(value, result);
        }

        [Fact]
        public void Set_WithCustomExpiration_SetsValue()
        {
            var key = "testKey";
            var value = "testValue";
            var expiration = TimeSpan.FromSeconds(30);

            _cacheManager.Set(key, value, expiration);
            var result = _cacheManager.Get<string>(key);

            Assert.Equal(value, result);
        }

        [Fact]
        public void Set_InvalidKey_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => _cacheManager.Set(null, "value"));
            Assert.Throws<ArgumentException>(() => _cacheManager.Set("", "value"));
            Assert.Throws<ArgumentException>(() => _cacheManager.Set("   ", "value"));
        }

        [Fact]
        public void Set_InvalidValue_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => _cacheManager.Set("key", null));
        }

        [Fact]
        public void Set_InvalidExpiration_ThrowsArgumentException()
        {
            Assert.Throws<ArgumentException>(() => _cacheManager.Set("key", "value", TimeSpan.Zero));
            Assert.Throws<ArgumentException>(() => _cacheManager.Set("key", "value", TimeSpan.FromSeconds(-1)));
        }

        [Fact]
        public async Task SetAsync_WithCustomExpiration_SetsValue()
        {
            var key = "testKey";
            var value = "testValue";
            var expiration = TimeSpan.FromSeconds(30);

            await _cacheManager.SetAsync(key, value, expiration);
            var result = await _cacheManager.GetAsync<string>(key);

            Assert.Equal(value, result);
        }

        [Fact]
        public void Remove_KeyExists_RemovesValue()
        {
            var key = "testKey";
            var value = "testValue";

            _cacheManager.Set(key, value);
            var removed = _cacheManager.Remove(key);
            var result = _cacheManager.Get<string>(key);

            Assert.True(removed);
            Assert.Null(result);
        }

        [Fact]
        public void Remove_KeyNotExists_ReturnsFalse()
        {
            var removed = _cacheManager.Remove("nonexistentKey");

            Assert.False(removed);
        }

        [Fact]
        public async Task RemoveAsync_KeyExists_RemovesValue()
        {
            var key = "testKey";
            var value = "testValue";

            await _cacheManager.SetAsync(key, value);
            var removed = await _cacheManager.RemoveAsync(key);
            var result = await _cacheManager.GetAsync<string>(key);

            Assert.True(removed);
            Assert.Null(result);
        }

        [Fact]
        public void Contains_KeyExists_ReturnsTrue()
        {
            var key = "testKey";
            var value = "testValue";

            _cacheManager.Set(key, value);
            var contains = _cacheManager.Contains(key);

            Assert.True(contains);
        }

        [Fact]
        public void Contains_KeyNotExists_ReturnsFalse()
        {
            var contains = _cacheManager.Contains("nonexistentKey");

            Assert.False(contains);
        }

        [Fact]
        public async Task ContainsAsync_KeyExists_ReturnsTrue()
        {
            var key = "testKey";
            var value = "testValue";

            await _cacheManager.SetAsync(key, value);
            var contains = await _cacheManager.ContainsAsync(key);

            Assert.True(contains);
        }

        [Fact]
        public void GetOrCreate_KeyExists_ReturnsCachedValue()
        {
            var key = "testKey";
            var originalValue = "originalValue";

            _cacheManager.Set(key, originalValue);
            var result = _cacheManager.GetOrCreate(key, () => "newValue");

            Assert.Equal(originalValue, result);
        }

        [Fact]
        public void GetOrCreate_KeyNotExists_CreatesAndReturnsValue()
        {
            var key = "testKey";
            var newValue = "newValue";

            var result = _cacheManager.GetOrCreate(key, () => newValue);
            var cachedValue = _cacheManager.Get<string>(key);

            Assert.Equal(newValue, result);
            Assert.Equal(newValue, cachedValue);
        }

        [Fact]
        public void GetOrCreate_WithCustomExpiration_SetsWithCustomExpiration()
        {
            var key = "testKey";
            var newValue = "newValue";
            var expiration = TimeSpan.FromSeconds(30);

            var result = _cacheManager.GetOrCreate(key, () => newValue, expiration);
            var cachedValue = _cacheManager.Get<string>(key);

            Assert.Equal(newValue, result);
            Assert.Equal(newValue, cachedValue);
        }

        [Fact]
        public async Task GetOrCreateAsync_KeyNotExists_CreatesAndReturnsValue()
        {
            var key = "testKey";
            var newValue = "newValue";

            var result = await _cacheManager.GetOrCreateAsync(key, () => Task.FromResult(newValue));
            var cachedValue = await _cacheManager.GetAsync<string>(key);

            Assert.Equal(newValue, result);
            Assert.Equal(newValue, cachedValue);
        }

        [Fact]
        public void Clear_RemovesAllItems()
        {
            _cacheManager.Set("key1", "value1");
            _cacheManager.Set("key2", "value2");
            _cacheManager.Set("key3", "value3");

            _cacheManager.Clear();

            Assert.Null(_cacheManager.Get<string>("key1"));
            Assert.Null(_cacheManager.Get<string>("key2"));
            Assert.Null(_cacheManager.Get<string>("key3"));
        }

        [Fact]
        public async Task ClearAsync_RemovesAllItems()
        {
            await _cacheManager.SetAsync("key1", "value1");
            await _cacheManager.SetAsync("key2", "value2");
            await _cacheManager.SetAsync("key3", "value3");

            await _cacheManager.ClearAsync();

            Assert.Null(await _cacheManager.GetAsync<string>("key1"));
            Assert.Null(await _cacheManager.GetAsync<string>("key2"));
            Assert.Null(await _cacheManager.GetAsync<string>("key3"));
        }

        [Fact]
        public void RemoveExpired_RemovesExpiredItems()
        {
            var key = "testKey";
            var value = "testValue";
            var shortExpiration = TimeSpan.FromMilliseconds(100);

            _cacheManager.Set(key, value, shortExpiration);

            // Wait for expiration
            Thread.Sleep(200);

            var removedCount = _cacheManager.RemoveExpired();
            var result = _cacheManager.Get<string>(key);

            Assert.Equal(1, removedCount);
            Assert.Null(result);
        }

        [Fact]
        public async Task RemoveExpiredAsync_RemovesExpiredItems()
        {
            var key = "testKey";
            var value = "testValue";
            var shortExpiration = TimeSpan.FromMilliseconds(100);

            await _cacheManager.SetAsync(key, value, shortExpiration);

            // Wait for expiration
            await Task.Delay(200);

            var removedCount = await _cacheManager.RemoveExpiredAsync();
            var result = await _cacheManager.GetAsync<string>(key);

            Assert.Equal(1, removedCount);
            Assert.Null(result);
        }

        [Fact]
        public void GenerateLicenseInfoKey_ReturnsCorrectKey()
        {
            var server = "testServer";
            var port = 27000;

            var key = _cacheManager.GenerateLicenseInfoKey(server, port);

            Assert.Equal($"LicenseInfo_{server}_{port}", key);
        }

        [Fact]
        public void GenerateLicenseFeatureKey_ReturnsCorrectKey()
        {
            var server = "testServer";
            var port = 27000;
            var feature = "solidworks";

            var key = _cacheManager.GenerateLicenseFeatureKey(server, port, feature);

            Assert.Equal($"LicenseFeature_{server}_{port}_{feature}", key);
        }

        [Fact]
        public void GenerateServerStatusKey_ReturnsCorrectKey()
        {
            var server = "testServer";
            var port = 27000;

            var key = _cacheManager.GenerateServerStatusKey(server, port);

            Assert.Equal($"ServerStatus_{server}_{port}", key);
        }

        [Fact]
        public void GetLicenseInfo_KeyNotExists_ReturnsNull()
        {
            var result = _cacheManager.GetLicenseInfo("testServer", 27000);

            Assert.Null(result);
        }

        [Fact]
        public void SetAndGetLicenseInfo_WorksCorrectly()
        {
            var server = "testServer";
            var port = 27000;
            var licenseInfo = new Dictionary<string, LicenseInfo>
            {
                { "user1", new LicenseInfo("user1", "solidworks", DateTime.Now, LicenseStatus.Active) },
                { "user2", new LicenseInfo("user2", "solidworks", DateTime.Now, LicenseStatus.Idle) }
            };

            _cacheManager.SetLicenseInfo(server, port, licenseInfo);
            var result = _cacheManager.GetLicenseInfo(server, port);

            Assert.NotNull(result);
            Assert.Equal(2, result.Count);
            Assert.Equal("user1", result["user1"].UserHost);
            Assert.Equal("user2", result["user2"].UserHost);
        }

        [Fact]
        public async Task SetAndGetLicenseInfoAsync_WorksCorrectly()
        {
            var server = "testServer";
            var port = 27000;
            var licenseInfo = new Dictionary<string, LicenseInfo>
            {
                { "user1", new LicenseInfo("user1", "solidworks", DateTime.Now, LicenseStatus.Active) }
            };

            await _cacheManager.SetLicenseInfoAsync(server, port, licenseInfo);
            var result = await _cacheManager.GetLicenseInfoAsync(server, port);

            Assert.NotNull(result);
            Assert.Single(result);
            Assert.Equal("user1", result["user1"].UserHost);
        }

        [Fact]
        public void GetLicenseFeature_KeyNotExists_ReturnsNull()
        {
            var result = _cacheManager.GetLicenseFeature("testServer", 27000, "solidworks");

            Assert.Null(result);
        }

        [Fact]
        public void SetAndGetLicenseFeature_WorksCorrectly()
        {
            var server = "testServer";
            var port = 27000;
            var feature = "solidworks";
            var licenseFeature = new LicenseFeature
            {
                Name = feature,
                TotalLicenses = 10,
                UsedLicenses = 5,
                AvailableLicenses = 5
            };

            _cacheManager.SetLicenseFeature(server, port, feature, licenseFeature);
            var result = _cacheManager.GetLicenseFeature(server, port, feature);

            Assert.NotNull(result);
            Assert.Equal(feature, result.Name);
            Assert.Equal(10, result.TotalLicenses);
            Assert.Equal(5, result.UsedLicenses);
            Assert.Equal(5, result.AvailableLicenses);
        }

        [Fact]
        public void GetServerStatus_KeyNotExists_ReturnsNull()
        {
            var result = _cacheManager.GetServerStatus("testServer", 27000);

            Assert.Null(result);
        }

        [Fact]
        public void SetAndGetServerStatus_WorksCorrectly()
        {
            var server = "testServer";
            var port = 27000;
            var serverStatus = new LicenseServerStatus
            {
                Server = server,
                Port = port,
                IsAvailable = true,
                LastChecked = DateTime.Now,
                ResponseTime = TimeSpan.FromMilliseconds(100)
            };

            _cacheManager.SetServerStatus(server, port, serverStatus);
            var result = _cacheManager.GetServerStatus(server, port);

            Assert.NotNull(result);
            Assert.Equal(server, result.Server);
            Assert.Equal(port, result.Port);
            Assert.True(result.IsAvailable);
        }

        [Fact]
        public void InvalidateServer_RemovesAllServerRelatedKeys()
        {
            var server = "testServer";
            var port = 27000;

            // Set various keys for the server
            _cacheManager.SetLicenseInfo(server, port, new Dictionary<string, LicenseInfo>());
            _cacheManager.SetLicenseFeature(server, port, "solidworks", new LicenseFeature { Name = "solidworks" });
            _cacheManager.SetServerStatus(server, port, new LicenseServerStatus { Server = server, Port = port });

            // Invalidate the server
            _cacheManager.InvalidateServer(server, port);

            // Verify all keys are removed
            Assert.Null(_cacheManager.GetLicenseInfo(server, port));
            Assert.Null(_cacheManager.GetLicenseFeature(server, port, "solidworks"));
            Assert.Null(_cacheManager.GetServerStatus(server, port));
        }

        [Fact]
        public void InvalidateFeature_RemovesFeatureRelatedKeys()
        {
            var server = "testServer";
            var port = 27000;
            var feature = "solidworks";

            // Set keys for the feature
            _cacheManager.SetLicenseInfo(server, port, new Dictionary<string, LicenseInfo>());
            _cacheManager.SetLicenseFeature(server, port, feature, new LicenseFeature { Name = feature });
            _cacheManager.SetServerStatus(server, port, new LicenseServerStatus { Server = server, Port = port });

            // Invalidate the feature
            _cacheManager.InvalidateFeature(server, port, feature);

            // Verify feature keys are removed but server status remains
            Assert.Null(_cacheManager.GetLicenseFeature(server, port, feature));
            Assert.NotNull(_cacheManager.GetServerStatus(server, port));
        }

        [Fact]
        public void Statistics_AreUpdatedCorrectly()
        {
            var key = "testKey";
            var value = "testValue";

            // Test cache miss
            var missResult = _cacheManager.Get<string>(key);
            Assert.Null(missResult);

            // Test cache hit
            _cacheManager.Set(key, value);
            var hitResult = _cacheManager.Get<string>(key);
            Assert.Equal(value, hitResult);

            // Verify statistics
            var stats = _cacheManager.Statistics;
            Assert.Equal(2, stats.TotalRequests);
            Assert.Equal(1, stats.CacheHits);
            Assert.Equal(1, stats.CacheMisses);
            Assert.Equal(0.5, stats.HitRatio);
            Assert.Equal(1, stats.TotalItemsAdded);
        }

        [Fact]
        public void ConcurrentAccess_WorksCorrectly()
        {
            const int threadCount = 10;
            const int operationsPerThread = 100;
            var exceptions = new System.Collections.Concurrent.ConcurrentQueue<Exception>();
            var keys = Enumerable.Range(0, operationsPerThread).Select(i => $"key_{i}").ToList();

            var tasks = Enumerable.Range(0, threadCount).Select(threadNum =>
            {
                return Task.Run(() =>
                {
                    try
                    {
                        foreach (var key in keys)
                        {
                            // Set value
                            _cacheManager.Set(key, $"value_{threadNum}_{key}");

                            // Get value
                            var value = _cacheManager.Get<string>(key);
                            Assert.NotNull(value);

                            // Remove value
                            _cacheManager.Remove(key);
                        }
                    }
                    catch (Exception ex)
                    {
                        exceptions.Enqueue(ex);
                    }
                });
            }).ToList();

            Task.WaitAll(tasks.ToArray());

            Assert.Empty(exceptions);
        }

        [Fact]
        public void GetOrCreate_WithNullFactory_ThrowsArgumentNullException()
        {
            Assert.Throws<ArgumentNullException>(() => _cacheManager.GetOrCreate("key", (Func<string>)null));
        }

        [Fact]
        public void SetExistingValue_UpdatesValueAndStatistics()
        {
            var key = "testKey";
            var initialValue = "initialValue";
            var updatedValue = "updatedValue";

            // Set initial value
            _cacheManager.Set(key, initialValue);
            var statsAfterSet = _cacheManager.Statistics.GetSummary();
            Assert.Equal(1, statsAfterSet.TotalItemsAdded);

            // Update value
            _cacheManager.Set(key, updatedValue);
            var statsAfterUpdate = _cacheManager.Statistics.GetSummary();

            // Verify value was updated
            var result = _cacheManager.Get<string>(key);
            Assert.Equal(updatedValue, result);

            // Statistics should reflect the update (items added remains the same)
            Assert.Equal(1, statsAfterUpdate.TotalItemsAdded);
            Assert.Equal(1, statsAfterUpdate.TotalItemsRemoved);
        }
    }
}