using Playnite.SDK;
using Playnite.SDK.Data;
using System;
using System.Collections.Generic;
using Playnite.SDK;
using Playnite.SDK.Data;
using System;
using System.Collections.Generic;

namespace SteamDeckProtonDb
{
    public class SteamDeckProtonDbSettings : ObservableObject, ISettings
    {
        private readonly SteamDeckProtonDb plugin;

        private bool enableSteamDeckCategories = true;
        private bool enableProtonDbCategories = true;
        private bool enableProtonDbLink = true;
        private bool enableTags = true;
        private int cacheTtlMinutes = 1440; // 24 hours default
        private string protonDbApiUrl = "https://www.protondb.com/api/v1/reports/summaries/{0}.json";
        private bool useFileCache = true;

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
                    EnableSteamDeckCategories = saved.EnableSteamDeckCategories;
                    EnableProtonDbCategories = saved.EnableProtonDbCategories;
                    EnableProtonDbLink = saved.EnableProtonDbLink;
                    EnableTags = saved.EnableTags;
                    CacheTtlMinutes = saved.CacheTtlMinutes;
                    ProtonDbApiUrl = saved.ProtonDbApiUrl;
                    UseFileCache = saved.UseFileCache;
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
                EnableSteamDeckCategories = editingClone.EnableSteamDeckCategories;
                EnableProtonDbCategories = editingClone.EnableProtonDbCategories;
                EnableProtonDbLink = editingClone.EnableProtonDbLink;
                EnableTags = editingClone.EnableTags;
                CacheTtlMinutes = editingClone.CacheTtlMinutes;
                ProtonDbApiUrl = editingClone.ProtonDbApiUrl;
                UseFileCache = editingClone.UseFileCache;
            }
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
            return errors.Count == 0;
        }
    }
}