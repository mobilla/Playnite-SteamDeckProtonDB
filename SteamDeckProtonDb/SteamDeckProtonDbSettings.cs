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
        public int ProtonDbRateLimitMs { get => protonDbRateLimitMs; set => SetValue(ref protonDbRateLimitMs, value); }
        public int SteamStoreRateLimitMs { get => steamStoreRateLimitMs; set => SetValue(ref steamStoreRateLimitMs, value); }
        public int DebugProgressDelayMs { get => debugProgressDelayMs; set => SetValue(ref debugProgressDelayMs, value); }

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