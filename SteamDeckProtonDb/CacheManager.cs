using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;

namespace SteamDeckProtonDb
{
    public class CacheEntry<T>
    {
        public T Data { get; set; }
        public DateTime CachedAt { get; set; }

        public bool IsExpired(int ttlMinutes) => DateTime.UtcNow > CachedAt.AddMinutes(ttlMinutes);
    }

    public interface ICacheManager
    {
        bool TryGetCached<T>(string key, int ttlMinutes, out T cachedData);
        void SetCached<T>(string key, T data);
        void Clear();
    }

    public class FileCacheManager : ICacheManager
    {
        private readonly string cacheDirectory;
        private readonly object lockObj = new object();

        public FileCacheManager(string cacheDirectory)
        {
            this.cacheDirectory = cacheDirectory ?? throw new ArgumentNullException(nameof(cacheDirectory));
            
            // Ensure cache directory exists
            if (!Directory.Exists(cacheDirectory))
            {
                Directory.CreateDirectory(cacheDirectory);
            }
        }

        public bool TryGetCached<T>(string key, int ttlMinutes, out T cachedData)
        {
            cachedData = default(T);
            if (string.IsNullOrEmpty(key))
            {
                return false;
            }

            try
            {
                lock (lockObj)
                {
                    var filePath = GetCacheFilePath(key);
                    if (!File.Exists(filePath))
                    {
                        return false;
                    }

                    var content = File.ReadAllText(filePath, Encoding.UTF8);
                    using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(content)))
                    {
                        var serializer = new DataContractJsonSerializer(typeof(CacheEntry<T>));
                        var entry = serializer.ReadObject(ms) as CacheEntry<T>;

                           if (entry == null || entry.IsExpired(ttlMinutes))
                        {
                            // Delete expired cache entry
                            if (File.Exists(filePath))
                            {
                                File.Delete(filePath);
                            }
                            return false;
                        }

                        cachedData = entry.Data;
                        return true;
                    }
                }
            }
            catch
            {
                // If cache read fails, return false and let the caller fetch fresh data
                return false;
            }
        }

        public void SetCached<T>(string key, T data)
        {
            if (string.IsNullOrEmpty(key) || data == null)
            {
                return;
            }

            try
            {
                lock (lockObj)
                {
                    var filePath = GetCacheFilePath(key);
                    var entry = new CacheEntry<T> { Data = data, CachedAt = DateTime.UtcNow };
                    
                    using (var ms = new MemoryStream())
                    {
                        var serializer = new DataContractJsonSerializer(typeof(CacheEntry<T>));
                        serializer.WriteObject(ms, entry);
                        var content = Encoding.UTF8.GetString(ms.ToArray());
                        File.WriteAllText(filePath, content, Encoding.UTF8);
                    }
                }
            }
            catch
            {
                // If cache write fails, silently continue without caching
            }
        }

        public void Clear()
        {
            try
            {
                lock (lockObj)
                {
                    if (Directory.Exists(cacheDirectory))
                    {
                        var files = Directory.GetFiles(cacheDirectory, "*.cache.json");
                        foreach (var file in files)
                        {
                            File.Delete(file);
                        }
                    }
                }
            }
            catch
            {
                // Silently fail if cache clear fails
            }
        }

        private string GetCacheFilePath(string key)
        {
            // Sanitize key for use as filename
            var sanitizedKey = System.Text.RegularExpressions.Regex.Replace(key, @"[<>:""/\\|?*]", "_");
            return Path.Combine(cacheDirectory, $"{sanitizedKey}.cache.json");
        }
    }

    public class InMemoryCacheManager : ICacheManager
    {
        private readonly Dictionary<string, object> cache = new Dictionary<string, object>();
        private readonly object lockObj = new object();

        public bool TryGetCached<T>(string key, int ttlMinutes, out T cachedData)
        {
            cachedData = default(T);
            if (string.IsNullOrEmpty(key))
            {
                return false;
            }

            try
            {
                lock (lockObj)
                {
                    if (cache.TryGetValue(key, out var cachedObj) && cachedObj is CacheEntry<T> entry)
                    {
                        if (!entry.IsExpired(ttlMinutes))
                        {
                            cachedData = entry.Data;
                            return true;
                        }
                        else
                        {
                            // Remove expired entry
                            cache.Remove(key);
                        }
                    }
                    return false;
                }
            }
            catch
            {
                return false;
            }
        }

        public void SetCached<T>(string key, T data)
        {
            if (string.IsNullOrEmpty(key) || data == null)
            {
                return;
            }

            try
            {
                lock (lockObj)
                {
                    var entry = new CacheEntry<T> { Data = data, CachedAt = DateTime.UtcNow };
                    cache[key] = entry;
                }
            }
            catch
            {
                // Silently fail if cache set fails
            }
        }

        public void Clear()
        {
            try
            {
                lock (lockObj)
                {
                    cache.Clear();
                }
            }
            catch
            {
                // Silently fail
            }
        }
    }
}
