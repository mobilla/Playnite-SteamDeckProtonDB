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
        private int? resolvedAppId;

        public override List<MetadataField> AvailableFields => new List<MetadataField> 
        { 
            MetadataField.Tags,
            MetadataField.Features,
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
            fetcher = new MetadataFetcher(protonClient, deckSource, cacheManager, settings.CacheTtlMinutes, settings.EnableProtonDbSync, settings.EnableSteamDeckSync);
            processor = new MetadataProcessor(settings);
            updater = new MetadataUpdater(plugin, settings);
        }

        // Override additional methods based on supported metadata fields.
        public override string GetDescription(GetMetadataFieldArgs args)
        {
            // Return null to preserve existing description; the plugin doesn't modify descriptions
            // Instead, metadata is provided via categories, tags, and links
            return null;
        }

        private int ResolveAppId()
        {
            if (resolvedAppId.HasValue)
            {
                return resolvedAppId.Value;
            }

            var logger = Playnite.SDK.LogManager.GetLogger();
            try
            {
                var appIdTask = plugin.TryGetAppIdAsync(options?.GameData);
                if (appIdTask.Wait(TimeSpan.FromSeconds(10)))
                {
                    resolvedAppId = appIdTask.Result;
                }
                else
                {
                    resolvedAppId = 0;
                    logger.Debug($"ResolveAppId timed out for game '{options?.GameData?.Name}'");
                }
            }
            catch (Exception ex)
            {
                resolvedAppId = 0;
                logger.Debug($"ResolveAppId failed for game '{options?.GameData?.Name}': {ex.Message}");
            }

            return resolvedAppId ?? 0;
        }

        private FetchResult FetchData(int appId)
        {
            var logger = Playnite.SDK.LogManager.GetLogger();
            
            // Try to get cached values first
            fetcher.TryGetCachedProtonDbSummary(appId, out var cachedProton);
            fetcher.TryGetCachedDeckCompatibility(appId, out var cachedDeck);

            // If no cache, fetch fresh data synchronously
            // Playnite will show its own progress dialog during metadata downloads
            if (cachedProton == null || cachedDeck == SteamDeckCompatibility.Unknown)
            {
                logger.Debug($"Fetching fresh data for appId: {appId}");
                try
                {
                    var fetchTask = fetcher.GetBothAsync(appId);
                    fetchTask.Wait(TimeSpan.FromSeconds(15)); // Reasonable timeout
                    if (fetchTask.IsCompleted)
                    {
                        cachedDeck = fetchTask.Result.Deck;
                        cachedProton = fetchTask.Result.Proton;
                    }
                }
                catch (Exception ex)
                {
                    logger.Debug($"Fetch failed for appId {appId}: {ex.Message}");
                }
            }
            else
            {
                logger.Debug($"Using cached data for appId: {appId}");
            }
            
            return new FetchResult { Deck = cachedDeck, Proton = cachedProton };
        }

        public override IEnumerable<Link> GetLinks(GetMetadataFieldArgs args)
        {
            var gameMeta = options.GameData;
            var appId = ResolveAppId();

            Playnite.SDK.LogManager.GetLogger().Debug($"GetLinks called for appId: {appId}");
            if (appId <= 0) return new List<Link>();

            try
            {
                var fetchResult = FetchData(appId);
                var mapping = processor.Map(appId, fetchResult.Deck, fetchResult.Proton);
                var links = new List<Link>();

                if (!string.IsNullOrEmpty(mapping.ProtonDbUrl))
                {
                    // Avoid returning a duplicate link if it already exists on the game.
                    var hasExisting = gameMeta?.Links != null && gameMeta.Links.Any(l =>
                        string.Equals(l.Url, mapping.ProtonDbUrl, StringComparison.OrdinalIgnoreCase) ||
                        (string.Equals(l.Name, "ProtonDB", StringComparison.OrdinalIgnoreCase) && string.Equals(l.Url, mapping.ProtonDbUrl, StringComparison.OrdinalIgnoreCase))
                    );

                    if (!hasExisting)
                    {
                        links.Add(new Link("ProtonDB", mapping.ProtonDbUrl));
                    }
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
            var appId = ResolveAppId();

            var logger = Playnite.SDK.LogManager.GetLogger();
            logger.Debug($"GetTags called for appId: {appId}");
            if (appId <= 0) return new List<MetadataProperty>();

            try
            {
                var fetchResult = FetchData(appId);
                logger.Debug($"Fetched data - Deck: {fetchResult.Deck}, Proton: {fetchResult.Proton?.Tier}");
                var mapping = processor.Map(appId, fetchResult.Deck, fetchResult.Proton);
                logger.Debug($"Mapped tags: {string.Join(", ", mapping.Tags)}");
                
                return mapping.Tags.Select(t => new MetadataNameProperty(t)).ToList();
            }
            catch (Exception ex)
            {
                logger.Debug("GetTags error: " + ex.Message);
            }

            return new List<MetadataProperty>();
        }

        public override IEnumerable<MetadataProperty> GetFeatures(GetMetadataFieldArgs args)
        {
            var gameMeta = options.GameData;
            var appId = ResolveAppId();

            var logger = Playnite.SDK.LogManager.GetLogger();
            logger.Debug($"GetFeatures called for appId: {appId}");
            if (appId <= 0) return new List<MetadataProperty>();

            try
            {
                var fetchResult = FetchData(appId);
                logger.Debug($"Fetched data - Deck: {fetchResult.Deck}, Proton: {fetchResult.Proton?.Tier}");
                var mapping = processor.Map(appId, fetchResult.Deck, fetchResult.Proton);
                logger.Debug($"Mapped features: {string.Join(", ", mapping.Features)}");
                
                return mapping.Features.Select(f => new MetadataNameProperty(f)).ToList();
            }
            catch (Exception ex)
            {
                logger.Debug("GetFeatures error: " + ex.Message);
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
        private readonly bool enableProtonDbSync;
        private readonly bool enableSteamDeckSync;
        private static readonly Playnite.SDK.ILogger logger = Playnite.SDK.LogManager.GetLogger();

        public MetadataFetcher(IProtonDbClient protonClient, ISteamDeckSource deckSource, ICacheManager cacheManager = null, int cacheTtlMinutes = 1440, bool enableProtonDbSync = true, bool enableSteamDeckSync = true)
        {
            this.protonClient = protonClient ?? throw new ArgumentNullException(nameof(protonClient));
            this.deckSource = deckSource ?? throw new ArgumentNullException(nameof(deckSource));
            this.cacheManager = cacheManager ?? new InMemoryCacheManager();
            this.cacheTtlMinutes = cacheTtlMinutes;
            this.enableProtonDbSync = enableProtonDbSync;
            this.enableSteamDeckSync = enableSteamDeckSync;
        }

        public async Task<ProtonDbResult> GetProtonDbSummaryAsync(int appId)
        {
            if (!enableProtonDbSync)
            {
                return null;
            }

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
            if (!enableSteamDeckSync)
            {
                return SteamDeckCompatibility.Unknown;
            }

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
            cached = null;
            if (!enableProtonDbSync)
            {
                return false;
            }
            var cacheKey = $"proton_{appId}";
            return cacheManager.TryGetCached<ProtonDbResult>(cacheKey, cacheTtlMinutes, out cached);
        }

        public bool TryGetCachedDeckCompatibility(int appId, out SteamDeckCompatibility cached)
        {
            cached = SteamDeckCompatibility.Unknown;
            if (!enableSteamDeckSync)
            {
                return false;
            }

            var cacheKey = $"deck_{appId}";
            return cacheManager.TryGetCached<SteamDeckCompatibility>(cacheKey, cacheTtlMinutes, out cached);
        }

        public async Task<FetchResult> GetBothAsync(int appId)
        {
            Task<ProtonDbResult> protonTask = enableProtonDbSync
                ? GetProtonDbSummaryAsync(appId)
                : Task.FromResult<ProtonDbResult>(null);
            Task<SteamDeckCompatibility> deckTask = enableSteamDeckSync
                ? GetSteamDeckCompatibilityAsync(appId)
                : Task.FromResult(SteamDeckCompatibility.Unknown);
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
        private readonly SteamDeckProtonDbSettings settings;

        public MetadataProcessor(SteamDeckProtonDbSettings settings = null)
        {
            this.settings = settings ?? new SteamDeckProtonDbSettings();
        }

        public MappingResult Map(int appId, SteamDeckCompatibility deck, ProtonDbResult proton)
        {
            var result = new MappingResult();

            // Map Steam Deck compatibility to tags/features (categories removed)
            switch (deck)
            {
                case SteamDeckCompatibility.Verified:
                    var verifiedTag = settings.SteamDeckTagPrefix + settings.SteamDeckVerifiedTag;
                    if (!result.Tags.Contains(verifiedTag))
                        result.Tags.Add(verifiedTag);
                    var verifiedFeature = settings.SteamDeckVerifiedFeature;
                    if (!result.Features.Contains(verifiedFeature))
                        result.Features.Add(verifiedFeature);
                    break;

                case SteamDeckCompatibility.Playable:
                    var playableTag = settings.SteamDeckTagPrefix + settings.SteamDeckPlayableTag;
                    if (!result.Tags.Contains(playableTag))
                        result.Tags.Add(playableTag);
                    var playableFeature = settings.SteamDeckPlayableFeature;
                    if (!result.Features.Contains(playableFeature))
                        result.Features.Add(playableFeature);
                    break;

                case SteamDeckCompatibility.Unsupported:
                    var unsupportedTag = settings.SteamDeckTagPrefix + settings.SteamDeckUnsupportedTag;
                    if (!result.Tags.Contains(unsupportedTag))
                        result.Tags.Add(unsupportedTag);
                    var unsupportedFeature = settings.SteamDeckUnsupportedFeature;
                    if (!result.Features.Contains(unsupportedFeature))
                        result.Features.Add(unsupportedFeature);
                    break;

                case SteamDeckCompatibility.Unknown:
                default:
                    break;
            }

            // Map ProtonDB tier to tags/features (categories removed)
            var tier = proton?.Tier ?? ProtonDbTier.Unknown;
            if (tier != ProtonDbTier.Unknown)
            {
                var tierName = GetProtonDbTierName(tier);
                var tierTag = $"{settings.ProtonDbTagPrefix}{tierName.ToLowerInvariant()}";
                if (!result.Tags.Contains(tierTag))
                    result.Tags.Add(tierTag);
                var tierFeature = $"{settings.ProtonDbFeaturePrefix}{tierName}";
                if (!result.Features.Contains(tierFeature))
                    result.Features.Add(tierFeature);
                result.ProtonDbUrl = proton?.Url;
            }

            return result;
        }

        private string GetProtonDbTierName(ProtonDbTier tier)
        {
            switch (tier)
            {
                case ProtonDbTier.Platinum:
                    return settings.ProtonDbPlatinumRating;
                case ProtonDbTier.Gold:
                    return settings.ProtonDbGoldRating;
                case ProtonDbTier.Silver:
                    return settings.ProtonDbSilverRating;
                case ProtonDbTier.Bronze:
                    return settings.ProtonDbBronzeRating;
                case ProtonDbTier.Plausible:
                    return settings.ProtonDbPlausibleRating;
                case ProtonDbTier.Borked:
                    return settings.ProtonDbBorkedRating;
                default:
                    return tier.ToString();
            }
        }
    }

    public class MetadataUpdater
    {
        private readonly SteamDeckProtonDb plugin;
        private readonly SteamDeckProtonDbSettings settings;
        private static readonly Playnite.SDK.ILogger logger = Playnite.SDK.LogManager.GetLogger();

        public MetadataUpdater(SteamDeckProtonDb plugin, SteamDeckProtonDbSettings settings = null)
        {
            this.plugin = plugin;
            this.settings = settings;
        }

        public void Apply(Game game, MappingResult result, bool dryRun = false)
        {
            // Applies the latest mapping to a Playnite game:
            // 1) Optionally removes stale plugin-owned tags/features that are no longer desired.
            // 2) Adds the mapped Steam Deck/ProtonDB tags, features, and ProtonDB link when enabled.
            //    User-created metadata is left untouched; only this plugin's prefixes are managed.
            if (game == null || result == null)
            {
                return;
            }

            logger.Debug($"Apply called for game '{game.Name}' - Tags enabled: {settings?.EnableTags}, Features enabled: {settings?.EnableFeatures}");
            logger.Debug($"MappingResult has {result.Tags?.Count ?? 0} tags and {result.Features?.Count ?? 0} features");

            if (dryRun)
            {
                // Log what would be done without modifying the game.
                System.Diagnostics.Debug.WriteLine($"[DRY RUN] Would add tags: {string.Join(", ", result.Tags)}");
                System.Diagnostics.Debug.WriteLine($"[DRY RUN] Would add features: {string.Join(", ", result.Features)}");
                if (!string.IsNullOrEmpty(result.ProtonDbUrl))
                {
                    System.Diagnostics.Debug.WriteLine($"[DRY RUN] Would add link to: {result.ProtonDbUrl}");
                }
                return;
            }

            var desiredTags = result.Tags ?? new List<string>();
            var desiredFeatures = result.Features ?? new List<string>();

            if (settings?.EnableTags == true)
            {
                // Remove only plugin-owned tags (steamdeck:/protondb:) that are no longer desired; keep user tags intact
                MetadataUpdaterHelpers.RemovePluginTags(game, plugin.PlayniteApi.Database.Tags, desiredTags, settings);
            }

            // Add tags to the game.
            if (desiredTags.Count > 0)
            {
                // Check if tags should be applied based on settings
                if (settings?.EnableTags != true)
                {
                    logger.Debug($"Skipping {desiredTags.Count} tags - not enabled in settings");
                }
                else
                {
                    logger.Debug($"Adding tags - game.Tags is null: {game.TagIds == null}");
                    var dbTags = plugin.PlayniteApi.Database.Tags;
                    foreach (var tagName in desiredTags)
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
                        if (game.TagIds == null || !game.TagIds.Contains(tag.Id))
                        {
                            if (game.TagIds == null) game.TagIds = new List<Guid>();
                            game.TagIds.Add(tag.Id);
                            logger.Debug($"Added tag '{tagName}' to game");
                        }
                    }
                }
            }

            if (settings?.EnableFeatures == true)
            {
                // Remove only plugin-owned features (Steamdeck*/Protondb*) that are no longer desired; keep user features intact
                MetadataUpdaterHelpers.RemovePluginFeatures(game, plugin.PlayniteApi.Database.Features, desiredFeatures, settings);
            }

            // Add features to the game.
            if (desiredFeatures.Count > 0)
            {
                // Check if features should be applied based on settings
                if (settings?.EnableFeatures != true)
                {
                    logger.Debug($"Skipping {desiredFeatures.Count} features - not enabled in settings");
                }
                else
                {
                    logger.Debug($"Adding features - game.FeatureIds is null: {game.FeatureIds == null}");
                    var dbFeatures = plugin.PlayniteApi.Database.Features;
                    foreach (var featureName in desiredFeatures)
                    {
                        // Find or create the feature.
                        var existingFeature = dbFeatures.FirstOrDefault(f => f.Name == featureName);
                        GameFeature feature = existingFeature;
                        if (feature == null)
                        {
                            feature = new GameFeature { Name = featureName };
                            dbFeatures.Add(feature);
                        }

                        // Add to game if not already present.
                        if (game.FeatureIds == null || !game.FeatureIds.Contains(feature.Id))
                        {
                            if (game.FeatureIds == null) game.FeatureIds = new List<Guid>();
                            game.FeatureIds.Add(feature.Id);
                            logger.Debug($"Added feature '{featureName}' to game");
                        }
                    }
                }
            }

            // Add link to ProtonDB if available.
            if (!string.IsNullOrEmpty(result.ProtonDbUrl))
            {
                // Check if ProtonDB link should be added based on settings
                if (settings?.EnableProtonDbLink != true)
                {
                    logger.Debug("Skipping ProtonDB link - not enabled in settings");
                    return;
                }

                // Avoid duplicate ProtonDB links.
                if (game.Links == null || !game.Links.Any(l => l.Name == "ProtonDB" && l.Url == result.ProtonDbUrl))
                {
                    if (game.Links == null) game.Links = new System.Collections.ObjectModel.ObservableCollection<Link>();
                    game.Links.Add(new Link("ProtonDB", result.ProtonDbUrl));
                    logger.Debug($"Added ProtonDB link to game");
                }
            }
        }
    }

    internal static class MetadataUpdaterHelpers
    {
        internal static void RemovePluginTags(Game game, IEnumerable<Tag> allTags, IEnumerable<string> desiredTags, SteamDeckProtonDbSettings settings)
        {
            if (game?.TagIds == null || !game.TagIds.Any() || allTags == null)
            {
                return;
            }

            var desired = new HashSet<string>(desiredTags ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            var tagLookup = allTags.Where(t => t != null).ToDictionary(t => t.Id, t => t);
            var removal = new List<Guid>();

            foreach (var tagId in game.TagIds.ToList())
            {
                if (!tagLookup.TryGetValue(tagId, out var tag) || string.IsNullOrEmpty(tag.Name))
                {
                    continue;
                }

                // Only strip plugin tags when they don't appear in the freshly computed desired set
                if (IsPluginTag(tag, settings) && !desired.Contains(tag.Name))
                {
                    removal.Add(tagId);
                }
            }

            if (removal.Count > 0)
            {
                game.TagIds = game.TagIds.Except(removal).ToList();
            }
        }

        private static bool IsPluginTag(Tag tag, SteamDeckProtonDbSettings settings)
        {
            var prefixes = new[]
            {
                settings?.SteamDeckTagPrefix ?? "steamdeck:",
                settings?.ProtonDbTagPrefix ?? "protondb:"
            }
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .ToList();

            if (!prefixes.Any())
            {
                return false; // No prefixes configured means we should not treat any tag as plugin-owned
            }

            return prefixes.Any(p => tag.Name.StartsWith(p, StringComparison.OrdinalIgnoreCase));
        }

        internal static void RemovePluginFeatures(Game game, IEnumerable<GameFeature> allFeatures, IEnumerable<string> desiredFeatures, SteamDeckProtonDbSettings settings)
        {
            if (game?.FeatureIds == null || !game.FeatureIds.Any() || allFeatures == null)
            {
                return;
            }

            var desired = new HashSet<string>(desiredFeatures ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            var featureLookup = allFeatures.Where(f => f != null).ToDictionary(f => f.Id, f => f);
            var removal = new List<Guid>();

            foreach (var featureId in game.FeatureIds.ToList())
            {
                if (!featureLookup.TryGetValue(featureId, out var feature) || string.IsNullOrEmpty(feature.Name))
                {
                    continue;
                }

                // Only strip plugin features when they don't appear in the freshly computed desired set
                if (IsPluginFeature(feature, settings) && !desired.Contains(feature.Name))
                {
                    removal.Add(featureId);
                }
            }

            if (removal.Count > 0)
            {
                game.FeatureIds = game.FeatureIds.Except(removal).ToList();
            }
        }

        private static bool IsPluginFeature(GameFeature feature, SteamDeckProtonDbSettings settings)
        {
            var protonPrefix = settings?.ProtonDbFeaturePrefix ?? "Protondb ";
            var deckFeatures = new[]
            {
                settings?.SteamDeckVerifiedFeature ?? "Steamdeck Verified",
                settings?.SteamDeckPlayableFeature ?? "Steamdeck Playable",
                settings?.SteamDeckUnsupportedFeature ?? "Steamdeck Unsupported"
            }
            .Where(df => !string.IsNullOrWhiteSpace(df))
            .ToList();

            var protonPrefixActive = !string.IsNullOrWhiteSpace(protonPrefix);

            var isDeckFeature = deckFeatures.Any(df => feature.Name.Equals(df, StringComparison.OrdinalIgnoreCase));
            var isProtonFeature = protonPrefixActive && feature.Name.StartsWith(protonPrefix, StringComparison.OrdinalIgnoreCase);

            return isDeckFeature || isProtonFeature;
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
        public List<string> Tags { get; set; } = new List<string>();
        public List<string> Features { get; set; } = new List<string>();
        public string ProtonDbUrl { get; set; }
    }
}