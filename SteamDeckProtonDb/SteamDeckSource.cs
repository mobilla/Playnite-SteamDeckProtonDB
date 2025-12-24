using System;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace SteamDeckProtonDb
{
    public interface ISteamDeckSource
    {
        Task<SteamDeckCompatibility> GetCompatibilityAsync(int appId, CancellationToken ct = default);
    }

    // Attempts to query the Steam store REST API for Steam Deck compatibility information.
    // This is best-effort: Steam does not always expose Deck compatibility; the adapter searches
    // for common Deck-related tokens in the JSON payload and returns a conservative result.
    public class LocalSteamDeckSource : ISteamDeckSource
    {
        private static readonly Lazy<HttpClient> sharedClient = new Lazy<HttpClient>(() =>
        {
            var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            client.DefaultRequestHeaders.Add("User-Agent", Constants.UserAgent);
            return client;
        });
        private readonly HttpClient http;
        private readonly IRateLimiterService rateLimiterService;
        private static readonly Playnite.SDK.ILogger logger = Playnite.SDK.LogManager.GetLogger();

        public LocalSteamDeckSource(HttpClient client = null, IRateLimiterService rateLimiterService = null)
        {
            http = client ?? sharedClient.Value;
            this.rateLimiterService = rateLimiterService ?? new RateLimiterService();
        }

        public async Task<SteamDeckCompatibility> GetCompatibilityAsync(int appId, CancellationToken ct = default)
        {
            if (appId <= 0) return SteamDeckCompatibility.Unknown;

            // Use the more reliable ajaxgetdeckappcompatibilityreport endpoint
            var url = $"https://store.steampowered.com/saleaction/ajaxgetdeckappcompatibilityreport?nAppID={appId}&l=en&cc=US";
            logger.Debug($"Steam Deck API call for appId {appId}: {url}");
            try
            {
                var resp = await rateLimiterService.ExecuteSteamStoreAsync(
                    async token => await http.GetAsync(url, token).ConfigureAwait(false),
                    ct).ConfigureAwait(false);

                logger.Debug($"Steam Deck API response for appId {appId}: {resp.StatusCode}");
                if (!resp.IsSuccessStatusCode)
                {
                    if ((int)resp.StatusCode >= 400 && (int)resp.StatusCode < 500)
                    {
                        logger.Debug($"Steam API returned client error {resp.StatusCode} for appId {appId}");
                        return SteamDeckCompatibility.Unknown;
                    }
                }

                var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(body))
                {
                    logger.Debug($"Steam API returned empty body for appId {appId}");
                    return SteamDeckCompatibility.Unknown;
                }

                return ParseSteamDeckCompatibility(body, appId);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                logger.Debug($"Steam API call cancelled for appId {appId}");
                throw;
            }
            catch (Polly.CircuitBreaker.BrokenCircuitException)
            {
                logger.Debug($"Steam API circuit breaker open - service temporarily unavailable for appId {appId}");
                return SteamDeckCompatibility.Unknown;
            }
            catch (Polly.Timeout.TimeoutRejectedException)
            {
                logger.Debug($"Steam API request timeout for appId {appId}");
                return SteamDeckCompatibility.Unknown;
            }
            catch (Exception ex)
            {
                logger.Debug($"GetCompatibilityAsync unexpected error: {ex.Message}");
                return SteamDeckCompatibility.Unknown;
            }
        }

        private static SteamDeckCompatibility ParseSteamDeckCompatibility(string body, int appId)
        {
            if (string.IsNullOrWhiteSpace(body))
                return SteamDeckCompatibility.Unknown;

            try
            {
                // Parse the JSON response from ajaxgetdeckappcompatibilityreport
                // Expected structure: { "success": 1, "results": { "resolved_category": 3 } }
                // resolved_category values: 3 = Verified, 2 = Playable, 1 = Unsupported, 0 = Unknown
                var match = Regex.Match(body, "\"resolved_category\"\\s*:\\s*(\\d+)");
                if (match.Success && int.TryParse(match.Groups[1].Value, out int category))
                {
                    switch (category)
                    {
                        case 3:
                            logger.Debug($"Steam Deck compatibility for appId {appId}: Verified (category=3)");
                            return SteamDeckCompatibility.Verified;
                        case 2:
                            logger.Debug($"Steam Deck compatibility for appId {appId}: Playable (category=2)");
                            return SteamDeckCompatibility.Playable;
                        case 1:
                            logger.Debug($"Steam Deck compatibility for appId {appId}: Unsupported (category=1)");
                            return SteamDeckCompatibility.Unsupported;
                        case 0:
                            logger.Debug($"Steam Deck compatibility for appId {appId}: Unknown (category=0)");
                            return SteamDeckCompatibility.Unknown;
                        default:
                            logger.Debug($"Steam Deck compatibility for appId {appId}: Unknown category value {category}");
                            return SteamDeckCompatibility.Unknown;
                    }
                }

                // Check if the response indicates success=1 but no results (untested game)
                if (Regex.IsMatch(body, "\"success\"\\s*:\\s*1") && !body.Contains("\"results\""))
                {
                    logger.Debug($"Steam Deck compatibility for appId {appId}: Unknown (no results in response)");
                    return SteamDeckCompatibility.Unknown;
                }

                logger.Debug($"Steam Deck compatibility for appId {appId}: Unknown (unable to parse response)");
                return SteamDeckCompatibility.Unknown;
            }
            catch (Exception ex)
            {
                logger.Debug($"ParseSteamDeckCompatibility error for appId {appId}: {ex.Message}");
                return SteamDeckCompatibility.Unknown;
            }
        }
    }

    // Placeholder web-based adapter — returns Unknown by default.
    public class WebSteamDeckSource : ISteamDeckSource
    {
        public Task<SteamDeckCompatibility> GetCompatibilityAsync(int appId, CancellationToken ct = default)
        {
            return Task.FromResult(SteamDeckCompatibility.Unknown);
        }
    }
}
