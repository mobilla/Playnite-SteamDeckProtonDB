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
        private static readonly HttpClient sharedClient = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        private readonly HttpClient http;
        private static readonly Playnite.SDK.ILogger logger = Playnite.SDK.LogManager.GetLogger();

        public LocalSteamDeckSource(HttpClient client = null)
        {
            http = client ?? sharedClient;
        }

        public async Task<SteamDeckCompatibility> GetCompatibilityAsync(int appId, CancellationToken ct = default)
        {
            if (appId <= 0) return SteamDeckCompatibility.Unknown;

            var url = $"https://store.steampowered.com/api/appdetails?appids={appId}&cc=us&l=en";
            logger.Debug($"Steam Deck API call for appId {appId}: {url}");
            int attempt = 0;
            while (true)
            {
                attempt++;
                try
                {
                    var resp = await http.GetAsync(url, ct).ConfigureAwait(false);
                    logger.Debug($"Steam Deck API response for appId {appId}: {resp.StatusCode}");
                    if (!resp.IsSuccessStatusCode)
                    {
                        if ((int)resp.StatusCode >= 400 && (int)resp.StatusCode < 500) return SteamDeckCompatibility.Unknown;
                    }

                    var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                    if (string.IsNullOrWhiteSpace(body)) return SteamDeckCompatibility.Unknown;

                    var lower = body.ToLowerInvariant();

                    // Look for explicit steam deck compatibility tokens.
                    if (System.Text.RegularExpressions.Regex.IsMatch(lower, "steam[_ ]?deck.*verified") || lower.Contains("steam deck verified"))
                    {
                        logger.Debug($"Steam Deck compatibility for appId {appId}: Verified");
                        return SteamDeckCompatibility.Verified;
                    }

                    if (System.Text.RegularExpressions.Regex.IsMatch(lower, "steam[_ ]?deck.*playable") || lower.Contains("playable"))
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
                    var m = System.Text.RegularExpressions.Regex.Match(body, "\"steam_deck_compatibility\"\\s*:\\s*\"(?<val>[^\"]+)\"", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
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
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    logger.Debug("LocalSteamDeckSource request error: " + ex.Message);
                }

                if (attempt >= 3) return SteamDeckCompatibility.Unknown;
                await Task.Delay(200 * (int)Math.Pow(2, attempt - 1), ct).ConfigureAwait(false);
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
