using System;
using System.Linq;
using LicenseReleaseService.LicenseManagement.Caching;
using Xunit;

namespace LicenseReleaseService.Tests.LicenseManagement.Caching
{
    /// <summary>
    /// Unit tests for CacheStatistics class
    /// </summary>
    public class CacheStatisticsTests
    {
        [Fact]
        public void Constructor_InitializesWithDefaultValues()
        {
            var stats = new CacheStatistics();

            Assert.Equal(0, stats.TotalRequests);
            Assert.Equal(0, stats.CacheHits);
            Assert.Equal(0, stats.CacheMisses);
            Assert.Equal(0.0, stats.HitRatio);
            Assert.Equal(0, stats.TotalItemsAdded);
            Assert.Equal(0, stats.TotalItemsRemoved);
            Assert.Equal(0, stats.TotalItemsEvicted);
            Assert.Equal(0, stats.TotalExpiredItemsRemoved);
            Assert.Equal(0, stats.TotalMemoryBytesUsed);
            Assert.Equal(TimeSpan.Zero, stats.AverageOperationTime);
            Assert.Equal(0, stats.CurrentItemCount);
            Assert.True(stats.Uptime > TimeSpan.Zero);
            Assert.Equal(DateTime.Now.Date, stats.StartTime.Date);
            Assert.Equal(DateTime.Now.Date, stats.LastResetTime.Date);
            Assert.Empty(stats.TypeStatistics);
            Assert.Empty(stats.RecentOperations);
            Assert.Equal(0.0, stats.RequestsPerSecond);
            Assert.Equal(0.0, stats.HitsPerSecond);
            Assert.Equal(0.0, stats.MissesPerSecond);
        }

        [Fact]
        public void RecordHit_IncrementsCountersCorrectly()
        {
            var stats = new CacheStatistics();
            var operationTime = TimeSpan.FromMilliseconds(10);

            stats.RecordHit("testKey", operationTime);

            Assert.Equal(1, stats.TotalRequests);
            Assert.Equal(1, stats.CacheHits);
            Assert.Equal(0, stats.CacheMisses);
            Assert.Equal(1.0, stats.HitRatio);
            Assert.Equal(operationTime, stats.AverageOperationTime);
            Assert.Equal(1, stats.RequestsPerSecond, 1);
            Assert.Equal(1, stats.HitsPerSecond, 1);
            Assert.Equal(0.0, stats.MissesPerSecond);
        }

        [Fact]
        public void RecordMiss_IncrementsCountersCorrectly()
        {
            var stats = new CacheStatistics();
            var operationTime = TimeSpan.FromMilliseconds(15);

            stats.RecordMiss("testKey", operationTime);

            Assert.Equal(1, stats.TotalRequests);
            Assert.Equal(0, stats.CacheHits);
            Assert.Equal(1, stats.CacheMisses);
            Assert.Equal(0.0, stats.HitRatio);
            Assert.Equal(operationTime, stats.AverageOperationTime);
            Assert.Equal(1, stats.RequestsPerSecond, 1);
            Assert.Equal(0.0, stats.HitsPerSecond);
            Assert.Equal(1, stats.MissesPerSecond, 1);
        }

        [Fact]
        public void RecordMultipleOperations_CalculatesCorrectRatios()
        {
            var stats = new CacheStatistics();

            stats.RecordHit("key1", TimeSpan.FromMilliseconds(10));
            stats.RecordHit("key2", TimeSpan.FromMilliseconds(15));
            stats.RecordMiss("key3", TimeSpan.FromMilliseconds(20));
            stats.RecordHit("key4", TimeSpan.FromMilliseconds(5));

            Assert.Equal(4, stats.TotalRequests);
            Assert.Equal(3, stats.CacheHits);
            Assert.Equal(1, stats.CacheMisses);
            Assert.Equal(0.75, stats.HitRatio);
        }

        [Fact]
        public void RecordAddition_IncrementsCorrectCounters()
        {
            var stats = new CacheStatistics();
            var sizeBytes = 1024L;

            stats.RecordAddition("testKey", sizeBytes);

            Assert.Equal(1, stats.TotalItemsAdded);
            Assert.Equal(sizeBytes, stats.TotalMemoryBytesUsed);
        }

        [Fact]
        public void RecordRemoval_IncrementsCorrectCounters()
        {
            var stats = new CacheStatistics();
            var sizeBytes = 1024L;

            stats.RecordRemoval("testKey", sizeBytes);

            Assert.Equal(1, stats.TotalItemsRemoved);
            Assert.Equal(0, stats.TotalMemoryBytesUsed);
        }

        [Fact]
        public void RecordEviction_IncrementsCorrectCounters()
        {
            var stats = new CacheStatistics();
            var sizeBytes = 2048L;

            stats.RecordEviction("testKey", sizeBytes);

            Assert.Equal(1, stats.TotalItemsEvicted);
            Assert.Equal(0, stats.TotalMemoryBytesUsed);
        }

        [Fact]
        public void RecordExpiration_IncrementsCorrectCounters()
        {
            var stats = new CacheStatistics();
            var sizeBytes = 512L;

            stats.RecordExpiration("testKey", sizeBytes);

            Assert.Equal(1, stats.TotalExpiredItemsRemoved);
            Assert.Equal(0, stats.TotalMemoryBytesUsed);
        }

        [Fact]
        public void UpdateItemCount_SetsCurrentItemCount()
        {
            var stats = new CacheStatistics();

            stats.UpdateItemCount(100);

            Assert.Equal(100, stats.CurrentItemCount);
        }

        [Fact]
        public void TypeStatistics_AreRecordedCorrectly()
        {
            var stats = new CacheStatistics();

            stats.RecordHit("key1", TimeSpan.FromMilliseconds(10), "LicenseInfo");
            stats.RecordMiss("key2", TimeSpan.FromMilliseconds(15), "LicenseInfo");
            stats.RecordHit("key3", TimeSpan.FromMilliseconds(5), "ServerStatus");

            var typeStats = stats.TypeStatistics;

            Assert.True(typeStats.ContainsKey("LicenseInfo"));
            Assert.True(typeStats.ContainsKey("ServerStatus"));

            var licenseInfoStats = typeStats["LicenseInfo"];
            Assert.Equal(2, licenseInfoStats.TotalRequests);
            Assert.Equal(1, licenseInfoStats.CacheHits);
            Assert.Equal(1, licenseInfoStats.CacheMisses);
            Assert.Equal(0.5, licenseInfoStats.HitRatio);
            Assert.Equal(TimeSpan.FromMilliseconds(15), licenseInfoStats.TotalOperationTime);

            var serverStatusStats = typeStats["ServerStatus"];
            Assert.Equal(1, serverStatusStats.TotalRequests);
            Assert.Equal(1, serverStatusStats.CacheHits);
            Assert.Equal(0, serverStatusStats.CacheMisses);
            Assert.Equal(1.0, serverStatusStats.HitRatio);
        }

        [Fact]
        public void RecentOperations_AreRecordedAndLimited()
        {
            var stats = new CacheStatistics();

            // Add more operations than the limit
            for (int i = 0; i < 1100; i++)
            {
                stats.RecordHit($"key{i}", TimeSpan.FromMilliseconds(i));
            }

            var recentOps = stats.RecentOperations;

            Assert.Equal(1000, recentOps.Count);
            Assert.Equal("key100", recentOps.First().Key);
            Assert.Equal("key1099", recentOps.Last().Key);
        }

        [Fact]
        public void RecentOperations_ContainCorrectData()
        {
            var stats = new CacheStatistics();
            var key = "testKey";
            var operationTime = TimeSpan.FromMilliseconds(25);

            stats.RecordHit(key, operationTime, "TestType");

            var recentOps = stats.RecentOperations;

            Assert.Single(recentOps);
            var operation = recentOps.First();
            Assert.Equal(CacheOperationType.Hit, operation.OperationType);
            Assert.Equal(key, operation.Key);
            Assert.True(operation.Success);
            Assert.Equal(operationTime, operation.OperationTime);
            Assert.Equal("TestType", GetCacheTypeFromKey(key));
        }

        [Fact]
        public void Reset_ClearsAllStatistics()
        {
            var stats = new CacheStatistics();

            stats.RecordHit("key1", TimeSpan.FromMilliseconds(10));
            stats.RecordMiss("key2", TimeSpan.FromMilliseconds(15));
            stats.RecordAddition("key3", 1024);
            stats.RecordRemoval("key4", 512);
            stats.UpdateItemCount(50);

            stats.Reset();

            Assert.Equal(0, stats.TotalRequests);
            Assert.Equal(0, stats.CacheHits);
            Assert.Equal(0, stats.CacheMisses);
            Assert.Equal(0.0, stats.HitRatio);
            Assert.Equal(0, stats.TotalItemsAdded);
            Assert.Equal(0, stats.TotalItemsRemoved);
            Assert.Equal(0, stats.TotalItemsEvicted);
            Assert.Equal(0, stats.TotalExpiredItemsRemoved);
            Assert.Equal(0, stats.TotalMemoryBytesUsed);
            Assert.Equal(TimeSpan.Zero, stats.AverageOperationTime);
            Assert.Equal(0, stats.CurrentItemCount);
            Assert.Empty(stats.TypeStatistics);
            Assert.Empty(stats.RecentOperations);
            Assert.NotEqual(stats.StartTime, stats.LastResetTime);
        }

        [Fact]
        public void GetSummary_ReturnsCorrectSummary()
        {
            var stats = new CacheStatistics();

            stats.RecordHit("key1", TimeSpan.FromMilliseconds(10));
            stats.RecordMiss("key2", TimeSpan.FromMilliseconds(20));
            stats.RecordAddition("key3", 1024);
            stats.UpdateItemCount(5);

            var summary = stats.GetSummary();

            Assert.Equal(2, summary.TotalRequests);
            Assert.Equal(1, summary.CacheHits);
            Assert.Equal(1, summary.CacheMisses);
            Assert.Equal(0.5, summary.HitRatio);
            Assert.Equal(1, summary.TotalItemsAdded);
            Assert.Equal(0, summary.TotalItemsRemoved);
            Assert.Equal(1024, summary.TotalMemoryBytesUsed);
            Assert.Equal(TimeSpan.FromMilliseconds(15), summary.AverageOperationTime);
            Assert.Equal(5, summary.CurrentItemCount);
            Assert.True(summary.Uptime > TimeSpan.Zero);
            Assert.True(summary.TypeStatistics.ContainsKey("default"));
        }

        [Fact]
        public void RequestsPerSecond_CalculatedCorrectlyOverTime()
        {
            var stats = new CacheStatistics();

            // Add some requests
            for (int i = 0; i < 10; i++)
            {
                stats.RecordHit($"key{i}", TimeSpan.FromMilliseconds(1));
            }

            // Wait a bit to get a more accurate calculation
            System.Threading.Thread.Sleep(100);

            var requestsPerSecond = stats.RequestsPerSecond;

            Assert.True(requestsPerSecond > 0);
        }

        [Fact]
        public void HitRatio_WithNoRequests_ReturnsZero()
        {
            var stats = new CacheStatistics();

            var hitRatio = stats.HitRatio;

            Assert.Equal(0.0, hitRatio);
        }

        [Fact]
        public void AverageOperationTime_WithNoRequests_ReturnsZero()
        {
            var stats = new CacheStatistics();

            var avgTime = stats.AverageOperationTime;

            Assert.Equal(TimeSpan.Zero, avgTime);
        }

        private string GetCacheTypeFromKey(string key)
        {
            if (key.StartsWith("LicenseInfo_"))
                return "LicenseInfo";
            if (key.StartsWith("LicenseFeature_"))
                return "LicenseFeature";
            if (key.StartsWith("ServerStatus_"))
                return "ServerStatus";
            return "default";
        }
    }
}