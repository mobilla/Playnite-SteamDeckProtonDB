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
        private readonly int maxRetries = 3;
        private readonly string apiUrlFormat;
        private static readonly ILogger logger = LogManager.GetLogger();
        private static readonly HttpClient sharedClient = new HttpClient() { Timeout = TimeSpan.FromSeconds(10) };

        public ProtonDbClient(HttpMessageHandler handler = null, string apiUrlFormat = null)
        {
            if (handler != null)
            {
                http = new HttpClient(handler, disposeHandler: false);
            }
            else
            {
                http = sharedClient;
            }

            apiUrlFormat = apiUrlFormat ?? "https://www.protondb.com/api/v1/reports/summaries/{0}.json";
            this.apiUrlFormat = apiUrlFormat;
            try
            {
                http.Timeout = TimeSpan.FromSeconds(10);
            }
            catch (Exception ex)
            {
                logger.Debug("Failed to set HttpClient timeout: " + ex.Message);
            }
        }

        public ProtonDbClient(HttpClient client, string apiUrlFormat = null)
        {
            http = client ?? throw new ArgumentNullException(nameof(client));
            this.apiUrlFormat = apiUrlFormat ?? "https://www.protondb.com/api/v1/reports/summaries/{0}.json";
        }

        public async Task<ProtonDbResult> GetGameSummaryAsync(int appId, CancellationToken ct = default)
        {
            if (appId <= 0) throw new ArgumentOutOfRangeException(nameof(appId));

            // ProtonDB JSON summary endpoint (community endpoints vary).
            var jsonUrl = string.Format(apiUrlFormat ?? "https://www.protondb.com/api/v1/reports/summaries/{0}.json", appId);
            var fallbackUrl = $"https://www.protondb.com/app/{appId}";
            logger.Debug($"ProtonDB API call for appId {appId}: {jsonUrl}");
            int attempt = 0;
            while (true)
            {
                attempt++;
                try
                {
                    // Call the JSON summary endpoint and parse tier.
                    var response = await http.GetAsync(jsonUrl, ct).ConfigureAwait(false);
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

                    // On 4xx/5xx, break or retry based on status code.
                    if ((int)response.StatusCode >= 400 && (int)response.StatusCode < 500)
                    {
                        return new ProtonDbResult { Tier = ProtonDbTier.Unknown, Url = fallbackUrl };
                    }
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    logger.Debug("GetGameSummaryAsync request error: " + ex.Message);
                }

                if (attempt >= maxRetries)
                {
                    return new ProtonDbResult { Tier = ProtonDbTier.Unknown, Url = fallbackUrl };
                }

                // Exponential backoff
                await Task.Delay(200 * (int)Math.Pow(2, attempt - 1), ct).ConfigureAwait(false);
            }
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
