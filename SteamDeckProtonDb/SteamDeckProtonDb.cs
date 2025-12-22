using Playnite.SDK;
using Playnite.SDK.Plugins;
using Playnite.SDK.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Controls;

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
                // Ensure the view has the plugin settings as DataContext so bindings work
                var view = new SteamDeckProtonDbSettingsView();
                view.DataContext = settings;
                return view;
        }

        public override IEnumerable<MainMenuItem> GetMainMenuItems(GetMainMenuItemsArgs args)
        {
            return new List<MainMenuItem>
            {
                new MainMenuItem
                {
                    Description = "Add Steam Deck/ProtonDB tags and link",
                    MenuSection = "@Steam Deck ProtonDB",
                    Action = _ => AddTagsAndLinksToGames(PlayniteApi?.MainView?.SelectedGames)
                }
            };
        }

        private MetadataFetcher BuildFetcher()
        {
            var currentSettings = settings ?? new SteamDeckProtonDbSettings(this);
            var protonClient = new ProtonDbClient(apiUrlFormat: currentSettings.ProtonDbApiUrl);
            var deckSource = new LocalSteamDeckSource();
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
            var updater = new MetadataUpdater(this);

            var progressOptions = new GlobalProgressOptions("Adding Steam Deck/ProtonDB tags and link")
            {
                IsIndeterminate = false,
                Cancelable = true
            };

            PlayniteApi.Dialogs.ActivateGlobalProgress(async progress =>
            {
                progress.ProgressMaxValue = targetGames.Count;
                int processed = 0;

                foreach (var game in targetGames)
                {
                    if (progress.CancelToken.IsCancellationRequested)
                    {
                        break;
                    }

                    progress.Text = $"Processing {game.Name}";
                    progress.CurrentProgressValue = processed++;

                    if (!int.TryParse(game.GameId, out var appId) || appId <= 0)
                    {
                        continue;
                    }

                    try
                    {
                        var fetchResult = await fetcher.GetBothAsync(appId).ConfigureAwait(false);
                        var mapping = processor.Map(appId, fetchResult.Deck, fetchResult.Proton);

                        updater.Apply(game, mapping);
                        PlayniteApi.Database.Games.Update(game);
                    }
                    catch (Exception ex)
                    {
                        logger.Debug($"Failed to update game '{game.Name}': {ex.Message}");
                    }
                }
            }, progressOptions);
        }
    }
}