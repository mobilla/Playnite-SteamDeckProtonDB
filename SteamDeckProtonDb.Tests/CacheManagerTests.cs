using NUnit.Framework;
using System;
using System.Threading.Tasks;

namespace SteamDeckProtonDb.Tests
{
    [TestFixture]
    public class CacheManagerTests
    {
        [Test]
        public void InMemoryCache_SetAndGet_ReturnsData()
        {
            var cache = new InMemoryCacheManager();
            var testData = new ProtonDbResult { Tier = ProtonDbTier.Gold, Url = "http://test.com" };
            
            cache.SetCached("test_key", testData);
            
            Assert.IsTrue(cache.TryGetCached<ProtonDbResult>("test_key", 1440, out var result));
            Assert.AreEqual(testData.Tier, result.Tier);
            Assert.AreEqual(testData.Url, result.Url);
        }

        [Test]
        public void InMemoryCache_ExpiredEntry_ReturnsNotFound()
        {
            var cache = new InMemoryCacheManager();
            var testData = new ProtonDbResult { Tier = ProtonDbTier.Silver, Url = "http://test.com" };
            
            cache.SetCached("test_key", testData);
            
               // Sleep a bit to ensure time has passed, then check with -1 TTL (expired)
               System.Threading.Thread.Sleep(10);
               // Use negative TTL to simulate expiration
               Assert.IsFalse(cache.TryGetCached<ProtonDbResult>("test_key", -1, out _));
        }

        [Test]
        public void InMemoryCache_MissingKey_ReturnsFalse()
        {
            var cache = new InMemoryCacheManager();
            
            Assert.IsFalse(cache.TryGetCached<ProtonDbResult>("nonexistent", 1440, out _));
        }

        [Test]
        public void InMemoryCache_Clear_RemovesAllEntries()
        {
            var cache = new InMemoryCacheManager();
            cache.SetCached("key1", new ProtonDbResult { Tier = ProtonDbTier.Platinum });
            cache.SetCached("key2", new ProtonDbResult { Tier = ProtonDbTier.Gold });
            
            cache.Clear();
            
            Assert.IsFalse(cache.TryGetCached<ProtonDbResult>("key1", 1440, out _));
            Assert.IsFalse(cache.TryGetCached<ProtonDbResult>("key2", 1440, out _));
        }

        [Test]
        public void InMemoryCache_SetNull_DoesNotCache()
        {
            var cache = new InMemoryCacheManager();
            
            cache.SetCached<ProtonDbResult>("test_key", null);
            
            Assert.IsFalse(cache.TryGetCached<ProtonDbResult>("test_key", 1440, out _));
        }

        [Test]
        public void FileCacheManager_SetAndGet_ReturnsData()
        {
            var tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "steam_deck_proton_cache_test_" + Guid.NewGuid());
            var cache = new FileCacheManager(tempDir);
            var testData = new ProtonDbResult { Tier = ProtonDbTier.Bronze, Url = "http://test.com" };
            
            try
            {
                cache.SetCached("test_key", testData);
                
                Assert.IsTrue(cache.TryGetCached<ProtonDbResult>("test_key", 1440, out var result));
                Assert.AreEqual(testData.Tier, result.Tier);
                Assert.AreEqual(testData.Url, result.Url);
            }
            finally
            {
                // Cleanup
                if (System.IO.Directory.Exists(tempDir))
                {
                    System.IO.Directory.Delete(tempDir, true);
                }
            }
        }

        [Test]
        public void FileCacheManager_InvalidKey_HandlesGracefully()
        {
            var tempDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "steam_deck_proton_cache_test_" + Guid.NewGuid());
            var cache = new FileCacheManager(tempDir);
            
            try
            {
                // This should not throw even with invalid characters
                cache.SetCached("key<>invalid|chars", new ProtonDbResult { Tier = ProtonDbTier.Plausible });
                Assert.IsTrue(cache.TryGetCached<ProtonDbResult>("key<>invalid|chars", 1440, out var result));
                Assert.IsNotNull(result);
            }
            finally
            {
                if (System.IO.Directory.Exists(tempDir))
                {
                    System.IO.Directory.Delete(tempDir, true);
                }
            }
        }
    }
}
