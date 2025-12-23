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

            var url = $"https://store.steampowered.com/api/appdetails?appids={appId}&cc=us&l=en";
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

            var lower = body.ToLowerInvariant();

            // Look for explicit steam deck compatibility tokens.
            if (Regex.IsMatch(lower, "steam[_ ]?deck.*verified") || lower.Contains("steam deck verified"))
            {
                logger.Debug($"Steam Deck compatibility for appId {appId}: Verified");
                return SteamDeckCompatibility.Verified;
            }

            if (Regex.IsMatch(lower, "steam[_ ]?deck.*playable") || lower.Contains("playable"))
            {
                logger.Debug($"Steam Deck compatibility for appId {appId}: Playable");
                return SteamDeckCompatibility.Playable;
            }

            if (lower.Contains("unsupported") || lower.Contains("not supported") || lower.Contains("not compatible"))
            {
                logger.Debug($"Steam Deck compatibility for appId {appId}: Unsupported");
                return SteamDeckCompatibility.Unsupported;
            }

            // Some store payloads may include a `steam_deck_compatibility` field or similar; try to extract it.
            var m = Regex.Match(body, "\"steam_deck_compatibility\"\\s*:\\s*\"(?<val>[^\"]+)\"", RegexOptions.IgnoreCase);
            if (m.Success)
            {
                var v = m.Groups["val"].Value.ToLowerInvariant();
                if (v.Contains("verified")) return SteamDeckCompatibility.Verified;
                if (v.Contains("playable")) return SteamDeckCompatibility.Playable;
                if (v.Contains("unsupported") || v.Contains("borked")) return SteamDeckCompatibility.Unsupported;
            }

            logger.Debug($"Steam Deck compatibility for appId {appId}: Unknown");
            return SteamDeckCompatibility.Unknown;
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
