using System;
using System.Net.Http;
using Playnite.SDK;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.IO;

namespace SteamDeckProtonDb
{
    public interface IProtonDbClient
    {
        Task<ProtonDbResult> GetGameSummaryAsync(int appId, CancellationToken ct = default);
    }

    public class ProtonDbClient : IProtonDbClient, IDisposable
    {
        private readonly HttpClient http;
        private readonly string apiUrlFormat;
        private readonly IRateLimiterService rateLimiterService;
        private static readonly ILogger logger = LogManager.GetLogger();
        private static readonly Lazy<HttpClient> sharedClient = new Lazy<HttpClient>(() =>
        {
            var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            client.DefaultRequestHeaders.Add("User-Agent", Constants.UserAgent);
            return client;
        });

        public ProtonDbClient(HttpMessageHandler handler = null, string apiUrlFormat = null, IRateLimiterService rateLimiterService = null)
        {
            if (handler != null)
            {
                http = new HttpClient(handler, disposeHandler: false) { Timeout = TimeSpan.FromSeconds(10) };
                http.DefaultRequestHeaders.Add("User-Agent", Constants.UserAgent);
            }
            else
            {
                http = sharedClient.Value;
            }

            apiUrlFormat = apiUrlFormat ?? "https://www.protondb.com/api/v1/reports/summaries/{0}.json";
            this.apiUrlFormat = apiUrlFormat;
            this.rateLimiterService = rateLimiterService ?? new RateLimiterService();
        }

        public ProtonDbClient(HttpClient client, string apiUrlFormat = null, IRateLimiterService rateLimiterService = null)
        {
            http = client ?? throw new ArgumentNullException(nameof(client));
            this.apiUrlFormat = apiUrlFormat ?? "https://www.protondb.com/api/v1/reports/summaries/{0}.json";
            this.rateLimiterService = rateLimiterService ?? new RateLimiterService();
        }

        public async Task<ProtonDbResult> GetGameSummaryAsync(int appId, CancellationToken ct = default)
        {
            if (appId <= 0) throw new ArgumentOutOfRangeException(nameof(appId));

            // ProtonDB JSON summary endpoint (community endpoints vary).
            var jsonUrl = string.Format(apiUrlFormat ?? "https://www.protondb.com/api/v1/reports/summaries/{0}.json", appId);
            var fallbackUrl = $"https://www.protondb.com/app/{appId}";
            logger.Debug($"ProtonDB API call for appId {appId}: {jsonUrl}");
            try
            {
                // Execute through rate limiter pipeline (handles retries, rate limiting, circuit breaker, timeout)
                var response = await rateLimiterService.ExecuteProtonDbAsync(
                    async token => await http.GetAsync(jsonUrl, token).ConfigureAwait(false),
                    ct
                ).ConfigureAwait(false);

                logger.Debug($"ProtonDB API response for appId {appId}: {response.StatusCode}");
                if (response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    logger.Debug($"ProtonDB API body for appId {appId}: {body.Substring(0, Math.Min(200, body.Length))}...");
                    try
                    {
                        var parsed = ParseJsonSummary(body);
                        parsed.Url = parsed.Url ?? fallbackUrl;
                        logger.Debug($"ProtonDB parsed tier for appId {appId}: {parsed.Tier}");
                        return parsed;
                    }
                    catch (Exception ex)
                    {
                        logger.Debug("ParseJsonSummary failed: " + ex.Message);
                        return new ProtonDbResult { Tier = ProtonDbTier.Unknown, Url = fallbackUrl };
                    }
                }

                // On 4xx (client error), return immediately without retry
                if ((int)response.StatusCode >= 400 && (int)response.StatusCode < 500)
                {
                    logger.Debug($"ProtonDB API returned client error {response.StatusCode} for appId {appId}");
                    return new ProtonDbResult { Tier = ProtonDbTier.Unknown, Url = fallbackUrl };
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                logger.Debug($"ProtonDB API call cancelled for appId {appId}");
                throw;
            }
            catch (Polly.CircuitBreaker.BrokenCircuitException)
            {
                logger.Debug($"ProtonDB circuit breaker open - service temporarily unavailable for appId {appId}");
                return new ProtonDbResult { Tier = ProtonDbTier.Unknown, Url = fallbackUrl };
            }
            catch (Polly.Timeout.TimeoutRejectedException)
            {
                logger.Debug($"ProtonDB API request timeout for appId {appId}");
                return new ProtonDbResult { Tier = ProtonDbTier.Unknown, Url = fallbackUrl };
            }
            catch (Exception ex)
            {
                logger.Debug($"GetGameSummaryAsync unexpected error: {ex.Message}");
                return new ProtonDbResult { Tier = ProtonDbTier.Unknown, Url = fallbackUrl };
            }

            // Fallback: if we reach here without a return, treat as unknown.
            return new ProtonDbResult { Tier = ProtonDbTier.Unknown, Url = fallbackUrl };
        }

        private ProtonDbResult ParseJsonSummary(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new ProtonDbResult { Tier = ProtonDbTier.Unknown };

            try
            {
                var serializer = new DataContractJsonSerializer(typeof(Dictionary<string, object>));
                using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(json)))
                {
                    var obj = serializer.ReadObject(ms) as Dictionary<string, object>;
                    if (obj == null) return new ProtonDbResult { Tier = ProtonDbTier.Unknown };

                    string tierStr = GetNestedString(obj, new[] { "tier", "best_guess", "tier_name" });
                    string url = GetNestedString(obj, new[] { "url", "profile_url", "link" });

                    var tier = ParseTier(tierStr);
                    if (tier == ProtonDbTier.Unknown && string.IsNullOrEmpty(url))
                    {
                        // Fallback to simple regex extraction for plain payloads
                        try
                        {
                            var tierMatch = System.Text.RegularExpressions.Regex.Match(json, "\"tier\"\\s*:\\s*\"(?<tier>[^\"]+)\"", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                            var urlMatch = System.Text.RegularExpressions.Regex.Match(json, "\"url\"\\s*:\\s*\"(?<url>[^\"]+)\"", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                            if (tierMatch.Success || urlMatch.Success)
                            {
                                var tierStr2 = tierMatch.Success ? tierMatch.Groups["tier"].Value : null;
                                var url2 = urlMatch.Success ? urlMatch.Groups["url"].Value : null;
                                var tier2 = ParseTier(tierStr2);
                                return new ProtonDbResult { Tier = tier2, Url = url2 };
                            }
                        }
                        catch { }
                    }

                    return new ProtonDbResult { Tier = tier, Url = url };
                }
            }
            catch (Exception ex)
            {
                logger.Debug("ParseJsonSummary failed: " + ex.Message);
                return new ProtonDbResult { Tier = ProtonDbTier.Unknown };
            }
        }

        /// <summary>
        /// Recursively searches for a string value in a nested dictionary or array structure using the provided keys.
        /// </summary>
        /// <param name="obj">The object to search through. Can be a Dictionary&lt;string, object&gt; or an object array.</param>
        /// <param name="keys">An array of key names to search for (case-insensitive matching).</param>
        /// <returns>
        /// The first matching string value found, or null if no match is found or if the input is invalid.
        /// The method first searches for keys at the top level, then recursively searches nested dictionaries and arrays.
        /// </returns>
        /// <remarks>
        /// The search algorithm:
        /// 1. First attempts to find any of the provided keys at the current level (case-insensitive)
        /// 2. If found and the value is a string, returns it immediately
        /// 3. If found and the value is another type, attempts to convert it to string
        /// 4. If not found at the current level, recursively searches all nested dictionaries and arrays
        /// 5. Returns the first match found during the recursive search
        /// </remarks>
        private static string GetNestedString(object obj, string[] keys)
        {
            if (obj == null || keys == null || keys.Length == 0) return null;
            if (obj is Dictionary<string, object> dict)
            {
                foreach (var k in keys)
                {
                    foreach (var key in dict.Keys)
                    {
                        if (string.Equals(key, k, StringComparison.OrdinalIgnoreCase))
                        {
                            var val = dict[key];
                            if (val == null) break;
                            if (val is string s) return s;
                            // If it's a nested object or primitive, attempt to convert to string
                            try { return val.ToString(); } catch { break; }
                        }
                    }
                }

                // If not found at top level, search nested dictionaries/arrays
                foreach (var kv in dict)
                {
                    if (kv.Value is Dictionary<string, object> childDict)
                    {
                        var found = GetNestedString(childDict, keys);
                        if (found != null) return found;
                    }
                    else if (kv.Value is object[] arr)
                    {
                        foreach (var item in arr)
                        {
                            var found = GetNestedString(item, keys);
                            if (found != null) return found;
                        }
                    }
                }
            }
            else if (obj is object[] arr)
            {
                foreach (var item in arr)
                {
                    var found = GetNestedString(item, keys);
                    if (found != null) return found;
                }
            }

            return null;
        }
        

        private static string TryGetString(Dictionary<string, object> dict, string key)
        {
            if (dict == null) return null;
            foreach (var k in dict.Keys)
            {
                if (string.Equals(k, key, StringComparison.OrdinalIgnoreCase))
                {
                    var val = dict[k];
                    return val?.ToString();
                }
            }
            return null;
        }

        private static ProtonDbTier ParseTier(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return ProtonDbTier.Unknown;
            s = s.Trim().ToLowerInvariant();
            switch (s)
            {
                case "platinum": return ProtonDbTier.Platinum;
                case "gold": return ProtonDbTier.Gold;
                case "silver": return ProtonDbTier.Silver;
                case "bronze": return ProtonDbTier.Bronze;
                case "plausible": return ProtonDbTier.Plausible;
                case "borked": return ProtonDbTier.Borked;
                default: return ProtonDbTier.Unknown;
            }
        }

        public void Dispose()
        {
            http?.Dispose();
        }
    }
}
