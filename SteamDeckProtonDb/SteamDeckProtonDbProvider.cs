using Playnite.SDK.Plugins;
using Playnite.SDK.Models;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SteamDeckProtonDb
{
    public class SteamDeckProtonDbProvider : OnDemandMetadataProvider
    {
        private readonly MetadataRequestOptions options;
        private readonly SteamDeckProtonDb plugin;
        private readonly MetadataFetcher fetcher;
        private readonly MetadataProcessor processor;
        private readonly MetadataUpdater updater;

        public override List<MetadataField> AvailableFields => new List<MetadataField> 
        { 
            MetadataField.Tags,
            MetadataField.Links
        };

        public SteamDeckProtonDbProvider(MetadataRequestOptions options, SteamDeckProtonDb plugin)
        {
            this.options = options;
            this.plugin = plugin;
                // Wire default clients; these can be replaced later for testing.
                // Load plugin settings and wire into clients (cache TTL and ProtonDB API URL).
                var settings = plugin.LoadPluginSettings<SteamDeckProtonDbSettings>() ?? new SteamDeckProtonDbSettings();
                var protonClient = new ProtonDbClient(apiUrlFormat: settings.ProtonDbApiUrl);
                var deckSource = new LocalSteamDeckSource();
                ICacheManager cacheManager;
                if (settings.UseFileCache)
                {
                    var cacheDir = Path.Combine(plugin.GetPluginUserDataPath(), "cache");
                    cacheManager = new FileCacheManager(cacheDir);
                }
                else
                {
                    cacheManager = new InMemoryCacheManager();
                }
                fetcher = new MetadataFetcher(protonClient, deckSource, cacheManager, settings.CacheTtlMinutes);
            processor = new MetadataProcessor();
            updater = new MetadataUpdater(plugin);
        }

        // Override additional methods based on supported metadata fields.
        public override string GetDescription(GetMetadataFieldArgs args)
        {
            // Return null to preserve existing description; the plugin doesn't modify descriptions
            // Instead, metadata is provided via categories, tags, and links
            return null;
        }

        public override IEnumerable<Link> GetLinks(GetMetadataFieldArgs args)
        {
            var gameMeta = options.GameData;
            int appId = 0;
            if (gameMeta != null && !string.IsNullOrEmpty(gameMeta.GameId))
            {
                int.TryParse(gameMeta.GameId, out appId);
            }

            Playnite.SDK.LogManager.GetLogger().Debug($"GetLinks called for appId: {appId}");
            if (appId <= 0) return new List<Link>();

            try
            {
                // Try to get cached values first
                fetcher.TryGetCachedProtonDbSummary(appId, out var cachedProton);
                fetcher.TryGetCachedDeckCompatibility(appId, out var cachedDeck);

                // If no cache, fetch fresh data (this blocks but ensures we have data)
                if (cachedProton == null || cachedDeck == SteamDeckCompatibility.Unknown)
                {
                    var fetchTask = fetcher.GetBothAsync(appId);
                    fetchTask.Wait(TimeSpan.FromSeconds(10)); // Wait up to 10 seconds for data
                    if (fetchTask.IsCompleted)
                    {
                        cachedDeck = fetchTask.Result.Deck;
                        cachedProton = fetchTask.Result.Proton;
                    }
                }

                var mapping = processor.Map(appId, cachedDeck, cachedProton);
                var links = new List<Link>();

                if (!string.IsNullOrEmpty(mapping.ProtonDbUrl))
                {
                    links.Add(new Link("ProtonDB", mapping.ProtonDbUrl));
                }

                return links;
            }
            catch (Exception ex)
            {
                Playnite.SDK.LogManager.GetLogger().Debug("GetLinks error: " + ex.Message);
            }

            return new List<Link>();
        }

        public override IEnumerable<MetadataProperty> GetTags(GetMetadataFieldArgs args)
        {
            var gameMeta = options.GameData;
            int appId = 0;
            if (gameMeta != null && !string.IsNullOrEmpty(gameMeta.GameId))
            {
                int.TryParse(gameMeta.GameId, out appId);
            }

            Playnite.SDK.LogManager.GetLogger().Debug($"GetTags called for appId: {appId}");
            if (appId <= 0) return new List<MetadataProperty>();

            try
            {
                // Try to get cached values first
                fetcher.TryGetCachedProtonDbSummary(appId, out var cachedProton);
                fetcher.TryGetCachedDeckCompatibility(appId, out var cachedDeck);

                // If no cache, fetch fresh data
                if (cachedProton == null || cachedDeck == SteamDeckCompatibility.Unknown)
                {
                    var fetchTask = fetcher.GetBothAsync(appId);
                    fetchTask.Wait(TimeSpan.FromSeconds(10));
                    if (fetchTask.IsCompleted)
                    {
                        cachedDeck = fetchTask.Result.Deck;
                        cachedProton = fetchTask.Result.Proton;
                    }
                }

                Playnite.SDK.LogManager.GetLogger().Debug($"Fetched data - Deck: {cachedDeck}, Proton: {cachedProton?.Tier}");
                var mapping = processor.Map(appId, cachedDeck, cachedProton);
                Playnite.SDK.LogManager.GetLogger().Debug($"Mapped tags: {string.Join(", ", mapping.Tags)}");
                
                return mapping.Tags.Select(t => new MetadataNameProperty(t)).ToList();
            }
            catch (Exception ex)
            {
                Playnite.SDK.LogManager.GetLogger().Debug("GetTags error: " + ex.Message);
            }

            return new List<MetadataProperty>();
        }
    }

    // Lightweight skeletons for implementation planning and compilation.
    public class MetadataFetcher
    {
        private readonly IProtonDbClient protonClient;
        private readonly ISteamDeckSource deckSource;
           private readonly ICacheManager cacheManager;
           private readonly int cacheTtlMinutes;
           private static readonly Playnite.SDK.ILogger logger = Playnite.SDK.LogManager.GetLogger();

           public MetadataFetcher(IProtonDbClient protonClient, ISteamDeckSource deckSource, ICacheManager cacheManager = null, int cacheTtlMinutes = 1440)
        {
            this.protonClient = protonClient ?? throw new ArgumentNullException(nameof(protonClient));
            this.deckSource = deckSource ?? throw new ArgumentNullException(nameof(deckSource));
                this.cacheManager = cacheManager ?? new InMemoryCacheManager();
                this.cacheTtlMinutes = cacheTtlMinutes;
        }

        public async Task<ProtonDbResult> GetProtonDbSummaryAsync(int appId)
        {
            var cacheKey = $"proton_{appId}";
            if (cacheManager.TryGetCached<ProtonDbResult>(cacheKey, cacheTtlMinutes, out var cached))
            {
                return cached;
            }

            try
            {
                var result = await protonClient.GetGameSummaryAsync(appId).ConfigureAwait(false);
                if (result != null)
                {
                    try { cacheManager.SetCached(cacheKey, result); } catch (Exception ex) { logger.Debug("Cache set failed: " + ex.Message); }
                }
                return result;
            }
            catch (Exception ex)
            {
                logger.Debug("GetProtonDbSummaryAsync failed: " + ex.Message);
                return null;
            }
        }

        public async Task<SteamDeckCompatibility> GetSteamDeckCompatibilityAsync(int appId)
        {
            var cacheKey = $"deck_{appId}";
            if (cacheManager.TryGetCached<SteamDeckCompatibility>(cacheKey, cacheTtlMinutes, out var cached))
            {
                return cached;
            }

            try
            {
                var result = await deckSource.GetCompatibilityAsync(appId).ConfigureAwait(false);
                try { cacheManager.SetCached(cacheKey, result); } catch (Exception ex) { logger.Debug("Cache set failed: " + ex.Message); }
                return result;
            }
            catch (Exception ex)
            {
                logger.Debug("GetSteamDeckCompatibilityAsync failed: " + ex.Message);
                return SteamDeckCompatibility.Unknown;
            }
        }

        public bool TryGetCachedProtonDbSummary(int appId, out ProtonDbResult cached)
        {
            var cacheKey = $"proton_{appId}";
            return cacheManager.TryGetCached<ProtonDbResult>(cacheKey, cacheTtlMinutes, out cached);
        }

        public bool TryGetCachedDeckCompatibility(int appId, out SteamDeckCompatibility cached)
        {
            var cacheKey = $"deck_{appId}";
            return cacheManager.TryGetCached<SteamDeckCompatibility>(cacheKey, cacheTtlMinutes, out cached);
        }

        public async Task<FetchResult> GetBothAsync(int appId)
        {
            var protonTask = GetProtonDbSummaryAsync(appId);
            var deckTask = GetSteamDeckCompatibilityAsync(appId);
            await Task.WhenAll(protonTask, deckTask).ConfigureAwait(false);
            return new FetchResult { Deck = deckTask.Result, Proton = protonTask.Result };
        }
    }

    public class FetchResult
    {
        public SteamDeckCompatibility Deck { get; set; }
        public ProtonDbResult Proton { get; set; }
    }

    public class MetadataProcessor
    {
        public MetadataProcessor()
        {
        }

        public MappingResult Map(int appId, SteamDeckCompatibility deck, ProtonDbResult proton)
        {
            var result = new MappingResult();

            // Map Steam Deck compatibility to categories and tags
            switch (deck)
            {
                case SteamDeckCompatibility.Verified:
                    if (!result.Categories.Contains("Steam Deck")) result.Categories.Add("Steam Deck");
                    if (!result.Categories.Contains("Steam Deck - Verified")) result.Categories.Add("Steam Deck - Verified");
                    if (!result.Tags.Contains("steamdeck:verified")) result.Tags.Add("steamdeck:verified");
                    break;
                case SteamDeckCompatibility.Playable:
                    if (!result.Categories.Contains("Steam Deck")) result.Categories.Add("Steam Deck");
                    if (!result.Categories.Contains("Steam Deck - Playable")) result.Categories.Add("Steam Deck - Playable");
                    if (!result.Tags.Contains("steamdeck:playable")) result.Tags.Add("steamdeck:playable");
                    break;
                case SteamDeckCompatibility.Unsupported:
                    if (!result.Categories.Contains("Steam Deck")) result.Categories.Add("Steam Deck");
                    if (!result.Categories.Contains("Steam Deck - Unsupported")) result.Categories.Add("Steam Deck - Unsupported");
                    if (!result.Tags.Contains("steamdeck:unsupported")) result.Tags.Add("steamdeck:unsupported");
                    break;
                case SteamDeckCompatibility.Unknown:
                default:
                    break;
            }

            // Map ProtonDB tier to categories and tags
            var tier = proton?.Tier ?? ProtonDbTier.Unknown;
            if (tier != ProtonDbTier.Unknown)
            {
                var tierName = tier.ToString();
                if (!result.Categories.Contains("ProtonDB")) result.Categories.Add("ProtonDB");
                var tierCategory = $"ProtonDB - {tierName}";
                if (!result.Categories.Contains(tierCategory)) result.Categories.Add(tierCategory);
                var tierTag = $"protondb:{tierName.ToLowerInvariant()}";
                if (!result.Tags.Contains(tierTag)) result.Tags.Add(tierTag);
                result.ProtonDbUrl = proton?.Url;
            }

            return result;
        }
    }

    public class MetadataUpdater
    {
        private readonly SteamDeckProtonDb plugin;

        public MetadataUpdater(SteamDeckProtonDb plugin)
        {
            this.plugin = plugin;
        }

        public void Apply(Game game, MappingResult result, bool dryRun = false)
        {
            if (game == null || result == null)
            {
                return;
            }

            if (dryRun)
            {
                // Log what would be done without modifying the game.
                System.Diagnostics.Debug.WriteLine($"[DRY RUN] Would add categories: {string.Join(", ", result.Categories)}");
                System.Diagnostics.Debug.WriteLine($"[DRY RUN] Would add tags: {string.Join(", ", result.Tags)}");
                if (!string.IsNullOrEmpty(result.ProtonDbUrl))
                {
                    System.Diagnostics.Debug.WriteLine($"[DRY RUN] Would add link to: {result.ProtonDbUrl}");
                }
                return;
            }

            // Add categories to the game.
            if (result.Categories != null && result.Categories.Count > 0)
            {
                var dbCategories = plugin.PlayniteApi.Database.Categories;
                foreach (var catName in result.Categories)
                {
                    // Find or create the category.
                    var existingCat = dbCategories.FirstOrDefault(c => c.Name == catName);
                    Category cat = existingCat;
                    if (cat == null)
                    {
                        cat = new Category { Name = catName };
                        dbCategories.Add(cat);
                    }

                    // Add to game if not already present.
                    if (game.Categories != null && !game.Categories.Any(c => c.Id == cat.Id))
                    {
                        game.Categories.Add(cat);
                    }
                }
            }

            // Add tags to the game.
            if (result.Tags != null && result.Tags.Count > 0)
            {
                var dbTags = plugin.PlayniteApi.Database.Tags;
                foreach (var tagName in result.Tags)
                {
                    // Find or create the tag.
                    var existingTag = dbTags.FirstOrDefault(t => t.Name == tagName);
                    Tag tag = existingTag;
                    if (tag == null)
                    {
                        tag = new Tag { Name = tagName };
                        dbTags.Add(tag);
                    }

                    // Add to game if not already present.
                    if (game.Tags != null && !game.Tags.Any(t => t.Id == tag.Id))
                    {
                        game.Tags.Add(tag);
                    }
                }
            }

            // Add link to ProtonDB if available.
            if (!string.IsNullOrEmpty(result.ProtonDbUrl))
            {
                // Avoid duplicate ProtonDB links.
                if (game.Links != null && !game.Links.Any(l => l.Name == "ProtonDB" && l.Url == result.ProtonDbUrl))
                {
                    game.Links.Add(new Link("ProtonDB", result.ProtonDbUrl));
                }
            }
        }
    }

    public enum SteamDeckCompatibility
    {
        Unknown,
        Verified,
        Playable,
        Unsupported
    }

    public enum ProtonDbTier
    {
        Unknown,
        Platinum,
        Gold,
        Silver,
        Bronze,
        Plausible,
        Borked
    }

    public class ProtonDbResult
    {
        public ProtonDbTier Tier { get; set; } = ProtonDbTier.Unknown;
        public string Url { get; set; }
    }

    public class MappingResult
    {
        public List<string> Categories { get; set; } = new List<string>();
        public List<string> Tags { get; set; } = new List<string>();
        public string ProtonDbUrl { get; set; }
    }
}