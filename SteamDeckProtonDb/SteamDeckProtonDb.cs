using Playnite.SDK;
using Playnite.SDK.Plugins;
using Playnite.SDK.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Controls;
using System.Threading.Tasks;

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

                            if (!int.TryParse(game.GameId, out var appId) || appId <= 0)
                            {
                                progress.Text = $"[{processed + 1}/{targetGames.Count}] Skipping {game.Name} (no App ID)";
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
    }
}