using Playnite.SDK;
using Playnite.SDK.Data;
using System;
using System.Collections.Generic;
using System.IO;

namespace SteamDeckProtonDb
{
    public class SteamDeckProtonDbSettings : ObservableObject, ISettings
    {
        private readonly SteamDeckProtonDb plugin;

        private bool enableSteamDeckCategories = true;
        private bool enableProtonDbCategories = true;
        private bool enableProtonDbLink = true;
        private bool enableTags = true;
        private bool enableFeatures = false;
        private int cacheTtlMinutes = 1440; // 24 hours default
        private string protonDbApiUrl = "https://www.protondb.com/api/v1/reports/summaries/{0}.json";
        private bool useFileCache = true;
        private int protonDbRateLimitMs = 1000; // Default: ~60 req/min
        private int steamStoreRateLimitMs = 600; // Default: ~100 req/min
        private int debugProgressDelayMs = 0; // Optional artificial delay per item for debugging progress UI
        private bool autoFetchOnLibraryUpdate = false; // Auto-fetch status when games are added to library
        private DateTime? lastAutoLibUpdateTime = null; // Track last time auto-fetch ran on library update
        private bool tryNonSteamMatching = false; // Attempt to match non-Steam games to Steam App IDs
        private bool allowFuzzyNonSteamMatching = false; // Allow fuzzy matching when trying non-Steam
        
        // Steam Deck tag/feature names (configurable)
        private string steamDeckTagPrefix = "steamdeck:";
        private string steamDeckVerifiedTag = "verified";
        private string steamDeckPlayableTag = "playable";
        private string steamDeckUnsupportedTag = "unsupported";
        private string steamDeckVerifiedFeature = "Steamdeck Verified";
        private string steamDeckPlayableFeature = "Steamdeck Playable";
        private string steamDeckUnsupportedFeature = "Steamdeck Unsupported";
        
        // ProtonDB tag/feature names (configurable)
        private string protonDbTagPrefix = "protondb:";
        private string protonDbFeaturePrefix = "Protondb ";
        private string protonDbPlatinumRating = "Platinum";
        private string protonDbGoldRating = "Gold";
        private string protonDbSilverRating = "Silver";
        private string protonDbBronzeRating = "Bronze";
        private string protonDbPlausibleRating = "Plausible";
        private string protonDbBorkedRating = "Borked";
        
        public int ProtonDbRateLimitMs { get => protonDbRateLimitMs; set => SetValue(ref protonDbRateLimitMs, value); }
        public int SteamStoreRateLimitMs { get => steamStoreRateLimitMs; set => SetValue(ref steamStoreRateLimitMs, value); }
        public int DebugProgressDelayMs { get => debugProgressDelayMs; set => SetValue(ref debugProgressDelayMs, value); }
        public bool AutoFetchOnLibraryUpdate 
        { 
            get => autoFetchOnLibraryUpdate; 
            set 
            { 
                SetValue(ref autoFetchOnLibraryUpdate, value);
                // Initialize timestamp when auto-fetch is enabled to avoid bulk-updating existing library
                if (value)
                {
                    lastAutoLibUpdateTime = DateTime.Now;
                }
            } 
        }
        public DateTime? LastAutoLibUpdateTime { get => lastAutoLibUpdateTime; set => SetValue(ref lastAutoLibUpdateTime, value); }
        public bool TryNonSteamMatching { get => tryNonSteamMatching; set => SetValue(ref tryNonSteamMatching, value); }
        public bool AllowFuzzyNonSteamMatching { get => allowFuzzyNonSteamMatching; set => SetValue(ref allowFuzzyNonSteamMatching, value); }
        
        // Steam Deck tag/feature name properties
        public string SteamDeckTagPrefix { get => steamDeckTagPrefix; set => SetValue(ref steamDeckTagPrefix, value); }
        public string SteamDeckVerifiedTag { get => steamDeckVerifiedTag; set => SetValue(ref steamDeckVerifiedTag, value); }
        public string SteamDeckPlayableTag { get => steamDeckPlayableTag; set => SetValue(ref steamDeckPlayableTag, value); }
        public string SteamDeckUnsupportedTag { get => steamDeckUnsupportedTag; set => SetValue(ref steamDeckUnsupportedTag, value); }
        public string SteamDeckVerifiedFeature { get => steamDeckVerifiedFeature; set => SetValue(ref steamDeckVerifiedFeature, value); }
        public string SteamDeckPlayableFeature { get => steamDeckPlayableFeature; set => SetValue(ref steamDeckPlayableFeature, value); }
        public string SteamDeckUnsupportedFeature { get => steamDeckUnsupportedFeature; set => SetValue(ref steamDeckUnsupportedFeature, value); }
        
        // ProtonDB tag/feature name properties
        public string ProtonDbTagPrefix { get => protonDbTagPrefix; set => SetValue(ref protonDbTagPrefix, value); }
        public string ProtonDbFeaturePrefix { get => protonDbFeaturePrefix; set => SetValue(ref protonDbFeaturePrefix, value); }
        public string ProtonDbPlatinumRating { get => protonDbPlatinumRating; set => SetValue(ref protonDbPlatinumRating, value); }
        public string ProtonDbGoldRating { get => protonDbGoldRating; set => SetValue(ref protonDbGoldRating, value); }
        public string ProtonDbSilverRating { get => protonDbSilverRating; set => SetValue(ref protonDbSilverRating, value); }
        public string ProtonDbBronzeRating { get => protonDbBronzeRating; set => SetValue(ref protonDbBronzeRating, value); }
        public string ProtonDbPlausibleRating { get => protonDbPlausibleRating; set => SetValue(ref protonDbPlausibleRating, value); }
        public string ProtonDbBorkedRating { get => protonDbBorkedRating; set => SetValue(ref protonDbBorkedRating, value); }

        // Parameterless constructor required for Playnite deserialization
        public SteamDeckProtonDbSettings()
        {
        }

        public SteamDeckProtonDbSettings(SteamDeckProtonDb plugin) : this()
        {
            this.plugin = plugin;
            try
            {
                var saved = plugin?.LoadPluginSettings<SteamDeckProtonDbSettings>();
                if (saved != null)
                {
                    LoadFrom(saved);
                }
            }
            catch (Exception ex)
            {
                try { Playnite.SDK.LogManager.GetLogger().Error("Failed to load plugin settings: " + ex.Message); } catch { }
            }
        }

        public bool EnableSteamDeckCategories { get => enableSteamDeckCategories; set => SetValue(ref enableSteamDeckCategories, value); }
        public bool EnableProtonDbCategories { get => enableProtonDbCategories; set => SetValue(ref enableProtonDbCategories, value); }
        public bool EnableProtonDbLink { get => enableProtonDbLink; set => SetValue(ref enableProtonDbLink, value); }
        public bool EnableTags { get => enableTags; set => SetValue(ref enableTags, value); }
        public bool EnableFeatures { get => enableFeatures; set => SetValue(ref enableFeatures, value); }
        public int CacheTtlMinutes { get => cacheTtlMinutes; set => SetValue(ref cacheTtlMinutes, value); }
        public string ProtonDbApiUrl { get => protonDbApiUrl; set => SetValue(ref protonDbApiUrl, value); }
        public bool UseFileCache { get => useFileCache; set => SetValue(ref useFileCache, value); }

        private SteamDeckProtonDbSettings editingClone;

        public void BeginEdit()
        {
            editingClone = Serialization.GetClone(this);
        }

        public void CancelEdit()
        {
            if (editingClone != null)
            {
                LoadFrom(editingClone);
            }
        }

        private void LoadFrom(SteamDeckProtonDbSettings source)
        {
            EnableSteamDeckCategories = source.EnableSteamDeckCategories;
            EnableProtonDbCategories = source.EnableProtonDbCategories;
            EnableProtonDbLink = source.EnableProtonDbLink;
            EnableTags = source.EnableTags;
            EnableFeatures = source.EnableFeatures;
            CacheTtlMinutes = source.CacheTtlMinutes;
            ProtonDbApiUrl = source.ProtonDbApiUrl;
            UseFileCache = source.UseFileCache;
            ProtonDbRateLimitMs = source.ProtonDbRateLimitMs;
            SteamStoreRateLimitMs = source.SteamStoreRateLimitMs;
            DebugProgressDelayMs = source.DebugProgressDelayMs;
            AutoFetchOnLibraryUpdate = source.AutoFetchOnLibraryUpdate;
            LastAutoLibUpdateTime = source.LastAutoLibUpdateTime;
            TryNonSteamMatching = source.TryNonSteamMatching;
            AllowFuzzyNonSteamMatching = source.AllowFuzzyNonSteamMatching;
            
            // Load Steam Deck tag/feature names
            SteamDeckTagPrefix = source.SteamDeckTagPrefix;
            SteamDeckVerifiedTag = source.SteamDeckVerifiedTag;
            SteamDeckPlayableTag = source.SteamDeckPlayableTag;
            SteamDeckUnsupportedTag = source.SteamDeckUnsupportedTag;
            SteamDeckVerifiedFeature = source.SteamDeckVerifiedFeature;
            SteamDeckPlayableFeature = source.SteamDeckPlayableFeature;
            SteamDeckUnsupportedFeature = source.SteamDeckUnsupportedFeature;
            
            // Load ProtonDB tag/feature names
            ProtonDbTagPrefix = source.ProtonDbTagPrefix;
            ProtonDbFeaturePrefix = source.ProtonDbFeaturePrefix;
            ProtonDbPlatinumRating = source.ProtonDbPlatinumRating;
            ProtonDbGoldRating = source.ProtonDbGoldRating;
            ProtonDbSilverRating = source.ProtonDbSilverRating;
            ProtonDbBronzeRating = source.ProtonDbBronzeRating;
            ProtonDbPlausibleRating = source.ProtonDbPlausibleRating;
            ProtonDbBorkedRating = source.ProtonDbBorkedRating;
        }

        public void EndEdit()
        {
            try
            {
                plugin?.SavePluginSettings(this);
            }
            catch (Exception ex)
            {
                try { Playnite.SDK.LogManager.GetLogger().Error("Failed to save plugin settings: " + ex.Message); } catch { }
            }
        }

        public bool VerifySettings(out List<string> errors)
        {
            errors = new List<string>();
            if (CacheTtlMinutes < 0) errors.Add("Cache TTL must be non-negative.");
            if (ProtonDbRateLimitMs < 100) errors.Add("ProtonDB rate limit must be at least 100ms.");
            if (SteamStoreRateLimitMs < 100) errors.Add("Steam Store rate limit must be at least 100ms.");
            if (DebugProgressDelayMs < 0) errors.Add("Debug progress delay must be non-negative.");
            return errors.Count == 0;
        }
    }
}