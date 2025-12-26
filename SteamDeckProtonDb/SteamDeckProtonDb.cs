using Playnite.SDK;
using Playnite.SDK.Plugins;
using Playnite.SDK.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Controls;
using System.Threading.Tasks;
using Playnite.SDK.Events;

namespace SteamDeckProtonDb
{
    public class SteamDeckProtonDb : MetadataPlugin
    {
        private static readonly ILogger logger = LogManager.GetLogger();

        private SteamDeckProtonDbSettings settings { get; set; }

        public override Guid Id { get; } = Guid.Parse("78f86cf5-abfd-47e9-b753-6b81c29132ed");

        public override List<MetadataField> SupportedFields { get; } = new List<MetadataField>
        {
            MetadataField.Tags,
            MetadataField.Links
        };

        public override string Name => "Steam Deck & ProtonDB";

        public SteamDeckProtonDb(IPlayniteAPI api) : base(api)
        {
            settings = new SteamDeckProtonDbSettings(this);
            Properties = new MetadataPluginProperties
            {
                HasSettings = true
            };
        }

        public override OnDemandMetadataProvider GetMetadataProvider(MetadataRequestOptions options)
        {
            return new SteamDeckProtonDbProvider(options, this);
        }

        public override ISettings GetSettings(bool firstRunSettings)
        {
            return settings;
        }

        public override UserControl GetSettingsView(bool firstRunSettings)
        {
            return new SteamDeckProtonDbSettingsView(this);
        }

        public override IEnumerable<MainMenuItem> GetMainMenuItems(GetMainMenuItemsArgs args)
        {
            var localization = PlayniteApi?.Resources?.GetString("LOC_SteamDeckProtonDb_MenuItem_Description") 
                            ?? "Add Steam Deck/ProtonDB tags and link";
            var missingOnlyLocalization = PlayniteApi?.Resources?.GetString("LOC_SteamDeckProtonDb_MenuItem_MissingOnly") 
                                      ?? "Add Steam Deck/ProtonDB tags and link (missing only)";
            var section = PlayniteApi?.Resources?.GetString("LOC_SteamDeckProtonDb_MenuItem_Section") 
                       ?? "@Steam Deck ProtonDB";
            
            return new List<MainMenuItem>
            {
                new MainMenuItem
                {
                    Description = localization,
                    MenuSection = section,
                    Action = _ => AddTagsAndLinksToGames(PlayniteApi?.MainView?.SelectedGames)
                },
                new MainMenuItem
                {
                    Description = missingOnlyLocalization,
                    MenuSection = section,
                    Action = _ => AddTagsAndLinksToGamesMissingData(PlayniteApi?.MainView?.SelectedGames)
                }
            };
        }

        private MetadataFetcher BuildFetcher()
        {
            var currentSettings = settings ?? new SteamDeckProtonDbSettings(this);
            var rateLimiter = new RateLimiterService(currentSettings.ProtonDbRateLimitMs, currentSettings.SteamStoreRateLimitMs);
            var protonClient = new ProtonDbClient(apiUrlFormat: currentSettings.ProtonDbApiUrl, rateLimiterService: rateLimiter);
            var deckSource = new LocalSteamDeckSource(rateLimiterService: rateLimiter);
            ICacheManager cacheManager;
            if (currentSettings.UseFileCache)
            {
                var cacheDir = Path.Combine(GetPluginUserDataPath(), "cache");
                cacheManager = new FileCacheManager(cacheDir);
            }
            else
            {
                cacheManager = new InMemoryCacheManager();
            }

            return new MetadataFetcher(protonClient, deckSource, cacheManager, currentSettings.CacheTtlMinutes);
        }

        private bool NeedsPluginUpdate(Game game)
        {
            if (game == null)
            {
                return false;
            }

            bool needsDeckCategories = settings?.EnableSteamDeckCategories == true && !HasSteamDeckCategory(game);
            bool needsProtonCategories = settings?.EnableProtonDbCategories == true && !HasProtonDbCategory(game);
            bool needsTags = settings?.EnableTags == true && !HasPluginTags(game);
            bool needsFeatures = settings?.EnableFeatures == true && !HasPluginFeatures(game);
            bool needsLink = settings?.EnableProtonDbLink == true && !HasProtonDbLink(game);

            return needsDeckCategories || needsProtonCategories || needsTags || needsFeatures || needsLink;
        }

        private bool HasSteamDeckCategory(Game game)
        {
            var categories = game?.Categories;
            if (categories == null)
            {
                return false;
            }

            return categories.Any(c =>
                c != null &&
                !string.IsNullOrEmpty(c.Name) &&
                (string.Equals(c.Name, "Steam Deck", StringComparison.OrdinalIgnoreCase) ||
                 c.Name.StartsWith("Steam Deck -", StringComparison.OrdinalIgnoreCase))
            );
        }

        private bool HasProtonDbCategory(Game game)
        {
            var categories = game?.Categories;
            if (categories == null)
            {
                return false;
            }

            return categories.Any(c =>
                c != null &&
                !string.IsNullOrEmpty(c.Name) &&
                (string.Equals(c.Name, "ProtonDB", StringComparison.OrdinalIgnoreCase) ||
                 c.Name.StartsWith("ProtonDB -", StringComparison.OrdinalIgnoreCase))
            );
        }

        private bool HasPluginTags(Game game)
        {
            if (game?.TagIds == null || !game.TagIds.Any())
            {
                return false;
            }

            var dbTags = PlayniteApi?.Database?.Tags;
            if (dbTags == null)
            {
                return false;
            }

            var tagPrefix1 = settings?.SteamDeckTagPrefix ?? "steamdeck:";
            var tagPrefix2 = settings?.ProtonDbTagPrefix ?? "protondb:";

            return dbTags
                .Where(t => t != null && game.TagIds.Contains(t.Id))
                .Any(t =>
                    !string.IsNullOrEmpty(t.Name) &&
                    (t.Name.StartsWith(tagPrefix1, StringComparison.OrdinalIgnoreCase) ||
                     t.Name.StartsWith(tagPrefix2, StringComparison.OrdinalIgnoreCase))
                );
        }

        private bool HasPluginFeatures(Game game)
        {
            if (game?.FeatureIds == null || !game.FeatureIds.Any())
            {
                return false;
            }

            var dbFeatures = PlayniteApi?.Database?.Features;
            if (dbFeatures == null)
            {
                return false;
            }

            var featurePrefix1 = settings?.SteamDeckVerifiedFeature ?? "Steamdeck ";
            var featurePrefix2 = settings?.ProtonDbFeaturePrefix ?? "Protondb ";

            return dbFeatures
                .Where(f => f != null && game.FeatureIds.Contains(f.Id))
                .Any(f =>
                    !string.IsNullOrEmpty(f.Name) &&
                    (f.Name.StartsWith(featurePrefix1, StringComparison.OrdinalIgnoreCase) ||
                     f.Name.StartsWith(featurePrefix2, StringComparison.OrdinalIgnoreCase))
                );
        }

        private bool HasProtonDbLink(Game game)
        {
            var links = game?.Links;
            if (links == null)
            {
                return false;
            }

            return links.Any(l => l != null && string.Equals(l.Name, "ProtonDB", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Try to extract Steam App ID from game links (e.g., Steam store URLs)
        /// </summary>
        private bool TryGetAppIdFromLinks(Game game, out int appId)
        {
            appId = 0;
            var links = game?.Links;
            if (links == null)
            {
                return false;
            }

            foreach (var link in links.Where(l => l != null && !string.IsNullOrEmpty(l.Url)))
            {
                // Match Steam store URLs like https://store.steampowered.com/app/123456
                var match = System.Text.RegularExpressions.Regex.Match(link.Url, @"store\.steampowered\.com/app/(\d+)");
                if (match.Success && int.TryParse(match.Groups[1].Value, out var parsedId) && parsedId > 0)
                {
                    appId = parsedId;
                    logger.Debug($"Found App ID {appId} in link: {link.Url}");
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Query Steam Store search API to find an App ID by game name (fallback for non-Steam games)
        /// Implements robust fuzzy matching:
        /// - Strips common articles ("the", "a", "an")
        /// - Uses a longer normalized prefix (up to 12 chars)
        /// - Requires numeric tokens in title to also appear in matched name
        /// - Evaluates all search results from Steam Store search
        /// </summary>
        private async System.Threading.Tasks.Task<int> TryGetAppIdFromSteamApiAsync(Game game)
        {
            if (game == null || string.IsNullOrEmpty(game.Name))
            {
                return 0;
            }

            string NormalizeTitle(string title)
            {
                var t = title.ToLowerInvariant();
                // Replace non-alphanumeric with space
                t = System.Text.RegularExpressions.Regex.Replace(t, "[^a-z0-9]+", " ");
                // Remove common articles as standalone words
                t = System.Text.RegularExpressions.Regex.Replace(t, "\\b(the|a|an)\\b", " ", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                // Collapse spaces
                t = System.Text.RegularExpressions.Regex.Replace(t, "\\s+", " ").Trim();
                return t;
            }

            System.Collections.Generic.List<string> ExtractNumbers(string title)
            {
                var nums = new System.Collections.Generic.List<string>();
                var m = System.Text.RegularExpressions.Regex.Matches(title, "\\d+");
                foreach (System.Text.RegularExpressions.Match mm in m)
                {
                    if (mm.Success) nums.Add(mm.Value);
                }
                return nums;
            }

            try
            {
                using (var client = new System.Net.Http.HttpClient())
                {
                    client.Timeout = System.TimeSpan.FromSeconds(10);
                    var searchTerm = System.Uri.EscapeDataString(game.Name);
                    var url = $"https://store.steampowered.com/api/storesearch/?term={searchTerm}&l=en&cc=US";
                    var response = await client.GetAsync(url);
                    
                    if (!response.IsSuccessStatusCode)
                    {
                        logger.Debug($"Steam API request failed: {response.StatusCode}");
                        return 0;
                    }

                    var json = await response.Content.ReadAsStringAsync();

                    // Collect all name/appid pairs from store search items
                    var all = System.Text.RegularExpressions.Regex.Matches(
                        json,
                        "\\\"id\\\"\\s*:\\s*(\\d+)\\s*,[^\\{]*?\\\"name\\\"\\s*:\\s*\\\"([^\\\"]+)\\\"",
                        System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.Singleline
                    );

                    if (all == null || all.Count == 0)
                    {
                        return 0;
                    }

                    // First pass: exact case-insensitive match
                    foreach (System.Text.RegularExpressions.Match item in all)
                    {
                        var candName = item.Groups[1].Value;
                        var candIdStr = item.Groups[2].Value;
                        if (string.Equals(candName, game.Name, System.StringComparison.OrdinalIgnoreCase) &&
                            int.TryParse(candIdStr, out var candId) && candId > 0)
                        {
                            logger.Info($"Found App ID {candId} via exact name match for: {game.Name}");
                            return candId;
                        }
                    }

                    // Second pass: normalized fuzzy prefix + numeric constraints
                    var normalizedTitle = NormalizeTitle(game.Name);
                    var prefixLen = System.Math.Min(12, normalizedTitle.Length);
                    var normalizedPrefix = normalizedTitle.Substring(0, prefixLen);
                    var numbersInTitle = ExtractNumbers(game.Name);

                    foreach (System.Text.RegularExpressions.Match item in all)
                    {
                        var candName = item.Groups[1].Value;
                        var candIdStr = item.Groups[2].Value;
                        if (!int.TryParse(candIdStr, out var candId) || candId <= 0)
                        {
                            continue;
                        }

                        var candNorm = NormalizeTitle(candName);

                        // Require prefix containment
                        if (!candNorm.Contains(normalizedPrefix))
                        {
                            continue;
                        }

                        // Require all numeric tokens from title to appear in candidate name
                        bool numbersOk = true;
                        if (numbersInTitle.Count > 0)
                        {
                            foreach (var num in numbersInTitle)
                            {
                                if (!candName.Contains(num))
                                {
                                    numbersOk = false;
                                    break;
                                }
                            }
                        }

                        if (!numbersOk)
                        {
                            continue;
                        }

                        logger.Info($"Found App ID {candId} via fuzzy match for: {game.Name} (candidate: {candName})");
                        return candId;
                    }
                }
            }
            catch (System.Exception ex)
            {
                logger.Debug($"Error querying Steam API for {game?.Name}: {ex.Message}");
            }

            return 0;
        }

        /// <summary>
        /// Get App ID using fallback chain: direct ID → links → Steam API
        /// </summary>
        private async System.Threading.Tasks.Task<int> TryGetAppIdAsync(Game game)
        {
            // Primary: direct GameId (Steam games)
            if (game != null && int.TryParse(game.GameId, out var directId) && directId > 0)
            {
                return directId;
            }

            // Respect setting: only attempt non-Steam matching if enabled
            if (settings?.TryNonSteamMatching != true)
            {
                return 0;
            }

            // Secondary: extract from links
            if (TryGetAppIdFromLinks(game, out var linkId))
            {
                return linkId;
            }

            // Tertiary: query Steam API by name
            var apiId = await TryGetAppIdFromSteamApiAsync(game);
            if (apiId > 0)
            {
                return apiId;
            }

            return 0;
        }

        private void AddTagsAndLinksToGamesMissingData(IEnumerable<Game> games)
        {
            var missingGames = games?.Where(NeedsPluginUpdate).ToList() ?? new List<Game>();
            if (!missingGames.Any())
            {
                var noMissingMessage = PlayniteApi?.Resources?.GetString("LOC_SteamDeckProtonDb_MenuItem_MissingOnly_Empty")
                                     ?? "Selected games already have Steam Deck/ProtonDB data.";
                PlayniteApi.Dialogs?.ShowMessage(noMissingMessage);
                return;
            }

            logger.Info($"Missing-only update - {missingGames.Count} of {games?.Count() ?? 0} selected games need data");
            AddTagsAndLinksToGames(missingGames);
        }

        private void AddTagsAndLinksToGames(IEnumerable<Game> games)
        {
            var targetGames = games?.ToList() ?? new List<Game>();
            if (!targetGames.Any())
            {
                PlayniteApi.Dialogs?.ShowMessage("Select one or more games first.");
                return;
            }

            logger.Info($"Starting bulk update for {targetGames.Count} games");

            var fetcher = BuildFetcher();
            var processor = new MetadataProcessor(settings);
            var updater = new MetadataUpdater(this, settings);

            var progressOptions = new GlobalProgressOptions("Adding Steam Deck/ProtonDB tags and link")
            {
                IsIndeterminate = false,
                Cancelable = true
            };

            logger.Info("Activating global progress dialog");
            
            PlayniteApi.Dialogs.ActivateGlobalProgress(progress =>
            {
                try
                {
                    logger.Info($"Progress dialog activated - processing {targetGames.Count} games");
                    progress.ProgressMaxValue = targetGames.Count;
                    progress.CurrentProgressValue = 0;
                    
                    int processed = 0;
                    var debugDelayMs = Math.Max(0, settings?.DebugProgressDelayMs ?? 0);

                    // Use BufferedUpdate to avoid flooding other plugins with events
                    using (PlayniteApi.Database.BufferedUpdate())
                    {
                        foreach (var game in targetGames)
                        {
                            if (progress.CancelToken.IsCancellationRequested)
                            {
                                logger.Info("Bulk update cancelled by user");
                                break;
                            }

                            // Try to get App ID from multiple sources
                            var appIdTask = TryGetAppIdAsync(game);
                            appIdTask.Wait();
                            var appId = appIdTask.Result;
                            if (appId <= 0)
                            {
                                progress.Text = $"[{processed + 1}/{targetGames.Count}] Skipping {game.Name} (no Steam App ID found)";
                                progress.CurrentProgressValue = ++processed;
                                continue;
                            }

                            try
                            {
                                // Check if data is already cached
                                bool hasProtonCached = fetcher.TryGetCachedProtonDbSummary(appId, out _);
                                bool hasDeckCached = fetcher.TryGetCachedDeckCompatibility(appId, out _);
                                bool isFullyCached = hasProtonCached && hasDeckCached;

                                progress.Text = $"[{processed + 1}/{targetGames.Count}] Fetching {game.Name}..." + 
                                              (isFullyCached ? " (cached)" : " (from API)");
                                logger.Debug($"Progress: {processed}/{targetGames.Count} - {game.Name}");

                                // Optional artificial delay to help debug progress pacing
                                if (debugDelayMs > 0)
                                {
                                    System.Threading.Thread.Sleep(debugDelayMs);
                                }
                                
                                var fetchTask = fetcher.GetBothAsync(appId);
                                fetchTask.Wait();
                                var fetchResult = fetchTask.Result;
                                var mapping = processor.Map(appId, fetchResult.Deck, fetchResult.Proton);

                                progress.Text = $"[{processed + 1}/{targetGames.Count}] Updating {game.Name}...";
                                updater.Apply(game, mapping);
                                PlayniteApi.Database.Games.Update(game);
                            }
                            catch (Exception ex)
                            {
                                logger.Debug($"Failed to update game '{game.Name}': {ex.Message}");
                                progress.Text = $"[{processed + 1}/{targetGames.Count}] Failed: {game.Name}";
                            }
                            finally
                            {
                                progress.CurrentProgressValue = ++processed;
                            }
                        }
                    }
                    // BufferedUpdate ends here - single event sent to all plugins
                    logger.Info($"Bulk update completed - processed {processed} games");
                }
                catch (Exception ex)
                {
                    logger.Error($"Error during bulk update: {ex}");
                    throw;
                }
            }, progressOptions);
            
            logger.Info("Progress dialog closed - operation complete");
        }

        public override void OnLibraryUpdated(OnLibraryUpdatedEventArgs args)
        {
            if (settings?.AutoFetchOnLibraryUpdate != true)
            {
                return;
            }

            logger.Info("Auto-fetch: Library updated event triggered");

            // Fetch metadata for newly added games in background without blocking UI
            Task.Run(() =>
            {
                try
                {
                    // Guard against first-run bulk update: if we've never tracked a previous run,
                    // set the timestamp and skip processing to avoid touching the whole library.
                    if (settings.LastAutoLibUpdateTime == null)
                    {
                        logger.Info("Auto-fetch: First run detected; skipping to avoid bulk update");
                        settings.LastAutoLibUpdateTime = DateTime.Now;
                        SavePluginSettings(settings);
                        return;
                    }

                    // Get games added since last auto-update
                    var newlyAddedGames = PlayniteApi?.Database?.Games
                        .Where(g => g != null && g.Added.HasValue && 
                                  g.Added > settings.LastAutoLibUpdateTime)
                        .ToList() ?? new List<Game>();

                    if (!newlyAddedGames.Any())
                    {
                        logger.Debug("Auto-fetch: No newly added games");
                        settings.LastAutoLibUpdateTime = DateTime.Now;
                        SavePluginSettings(settings);
                        return;
                    }

                    // Filter to games that need updating and have retrievable App IDs
                    // Note: We'll filter for App IDs during processing since Steam API lookup is async
                    var gamesToUpdate = newlyAddedGames.Where(g =>
                        NeedsPluginUpdate(g) &&
                        (!string.IsNullOrEmpty(g.GameId) || (g.Links != null && g.Links.Any()))
                    ).ToList();

                    if (!gamesToUpdate.Any())
                    {
                        logger.Debug($"Auto-fetch: {newlyAddedGames.Count} games added but none need metadata or have Steam IDs");
                        settings.LastAutoLibUpdateTime = DateTime.Now;
                        SavePluginSettings(settings);
                        return;
                    }

                    logger.Info($"Auto-fetch: Processing {gamesToUpdate.Count} newly added games (out of {newlyAddedGames.Count} total new games)");

                    // If this would update a large number of games, ask for confirmation.
                    const int bulkConfirmThreshold = 25;
                    if (gamesToUpdate.Count >= bulkConfirmThreshold)
                    {
                        var message = $"This will update {gamesToUpdate.Count} games automatically. Proceed?";
                        var result = PlayniteApi.Dialogs.ShowMessage(message, "Steam Deck & ProtonDB", System.Windows.MessageBoxButton.YesNo);
                        if (result != System.Windows.MessageBoxResult.Yes)
                        {
                            logger.Info("Auto-fetch: User declined bulk update; skipping");
                            settings.LastAutoLibUpdateTime = DateTime.Now;
                            SavePluginSettings(settings);
                            return;
                        }
                    }

                    var fetcher = BuildFetcher();
                    var processor = new MetadataProcessor(settings);
                    var updater = new MetadataUpdater(this, settings);

                    using (PlayniteApi.Database.BufferedUpdate())
                    {
                        foreach (var game in gamesToUpdate)
                        {
                            try
                            {
                                // Try to get App ID from multiple sources
                                var appIdTask = TryGetAppIdAsync(game);
                                appIdTask.Wait();
                                var appId = appIdTask.Result;
                                if (appId <= 0)
                                {
                                    logger.Debug($"Auto-fetch: Could not determine Steam App ID for {game.Name}");
                                    continue;
                                }

                                logger.Debug($"Auto-fetch: Fetching metadata for {game.Name} (App ID: {appId})");
                                var fetchTask = fetcher.GetBothAsync(appId);
                                fetchTask.Wait();
                                var fetchResult = fetchTask.Result;
                                var mapping = processor.Map(appId, fetchResult.Deck, fetchResult.Proton);
                                updater.Apply(game, mapping);
                                PlayniteApi.Database.Games.Update(game);
                            }
                            catch (Exception ex)
                            {
                                logger.Debug($"Auto-fetch: Failed to update game '{game.Name}': {ex.Message}");
                            }
                        }
                    }

                    // Update the timestamp after successful processing
                    settings.LastAutoLibUpdateTime = DateTime.Now;
                    SavePluginSettings(settings);
                    
                    logger.Info($"Auto-fetch: Completed processing {gamesToUpdate.Count} games");
                }
                catch (Exception ex)
                {
                    logger.Error($"Auto-fetch: Error during background update: {ex}");
                }
            });
        }
    }
}