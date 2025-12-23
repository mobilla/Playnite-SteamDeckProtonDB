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
            var section = PlayniteApi?.Resources?.GetString("LOC_SteamDeckProtonDb_MenuItem_Section") 
                       ?? "@Steam Deck ProtonDB";
            
            return new List<MainMenuItem>
            {
                new MainMenuItem
                {
                    Description = localization,
                    MenuSection = section,
                    Action = _ => AddTagsAndLinksToGames(PlayniteApi?.MainView?.SelectedGames)
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

        private void AddTagsAndLinksToGames(IEnumerable<Game> games)
        {
            var targetGames = games?.ToList() ?? new List<Game>();
            if (!targetGames.Any())
            {
                PlayniteApi.Dialogs?.ShowMessage("Select one or more games first.");
                return;
            }

            var fetcher = BuildFetcher();
            var processor = new MetadataProcessor();
                var updater = new MetadataUpdater(this, settings);

            var progressOptions = new GlobalProgressOptions("Adding Steam Deck/ProtonDB tags and link")
            {
                IsIndeterminate = false,
                Cancelable = true
            };

            PlayniteApi.Dialogs.ActivateGlobalProgress(async progress =>
            {
                progress.ProgressMaxValue = targetGames.Count;
                progress.CurrentProgressValue = 0;
                
                // Yield to UI thread to ensure progress dialog is visible
                await Task.Delay(100).ConfigureAwait(false);
                
                int processed = 0;
                var debugDelayMs = Math.Max(0, settings?.DebugProgressDelayMs ?? 0);

                foreach (var game in targetGames)
                {
                    if (progress.CancelToken.IsCancellationRequested)
                    {
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
                        logger.Debug($"Progress: {processed}/{targetGames.Count} - {game.Name} (delay: {debugDelayMs}ms)");

                        // Optional artificial delay to help debug progress pacing
                        if (debugDelayMs > 0)
                        {
                            try { await Task.Delay(debugDelayMs, progress.CancelToken).ConfigureAwait(false); } catch (TaskCanceledException) { }
                        }
                        
                        var fetchResult = await fetcher.GetBothAsync(appId).ConfigureAwait(false);
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
            }, progressOptions);
        }
    }
}