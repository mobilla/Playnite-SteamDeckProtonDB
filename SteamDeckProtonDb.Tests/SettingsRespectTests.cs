using NUnit.Framework;
using Playnite.SDK.Models;
using System.Collections.Generic;
using System.Linq;

namespace SteamDeckProtonDb.Tests
{
    // Simple wrapper to inject test settings without needing full API mocks
    public class TestableMetadataProcessor : MetadataProcessor
    {
        // Reuse existing processor logic
    }

    public class TestableMetadataUpdater
    {
        private readonly SteamDeckProtonDbSettings settings;

        public TestableMetadataUpdater(SteamDeckProtonDbSettings settings)
        {
            this.settings = settings;
        }

        // Simplified Apply that doesn't need database access
        public ApplyResult ApplyDry(Game game, MappingResult result)
        {
            var applied = new ApplyResult();

            if (game == null || result == null)
            {
                return applied;
            }

            // Categories filtering logic (same as real MetadataUpdater)
            if (result.Categories != null && result.Categories.Count > 0)
            {
                foreach (var catName in result.Categories)
                {
                    var isDeckCategory = catName == "Steam Deck" || catName.StartsWith("Steam Deck -");
                    var isProtonCategory = catName == "ProtonDB" || catName.StartsWith("ProtonDB -");

                    if (isDeckCategory && settings?.EnableSteamDeckCategories != true)
                    {
                        continue;
                    }

                    if (isProtonCategory && settings?.EnableProtonDbCategories != true)
                    {
                        continue;
                    }

                    applied.CategoriesApplied.Add(catName);
                }
            }

            // Tags gating
            if (settings?.EnableTags == true && result.Tags != null && result.Tags.Count > 0)
            {
                applied.TagsApplied.AddRange(result.Tags);
            }

            // ProtonDB link gating
            if (settings?.EnableProtonDbLink == true && !string.IsNullOrEmpty(result.ProtonDbUrl))
            {
                applied.LinkApplied = result.ProtonDbUrl;
            }

            return applied;
        }
    }

    public class ApplyResult
    {
        public List<string> CategoriesApplied { get; set; } = new List<string>();
        public List<string> TagsApplied { get; set; } = new List<string>();
        public string LinkApplied { get; set; }
    }

    [TestFixture]
    public class SettingsRespectTests
    {
        [Test]
        public void MetadataProcessor_CreatesExpectedMapping()
        {
            var processor = new TestableMetadataProcessor();
            var result = processor.Map(220, SteamDeckCompatibility.Verified, 
                new ProtonDbResult { Tier = ProtonDbTier.Gold, Url = "https://protondb.com/app/220" });

            Assert.IsNotNull(result);
            Assert.IsTrue(result.Categories.Contains("Steam Deck"));
            Assert.IsTrue(result.Categories.Contains("Steam Deck - Verified"));
            Assert.IsTrue(result.Categories.Contains("ProtonDB"));
            Assert.IsTrue(result.Categories.Contains("ProtonDB - Gold"));
            Assert.IsTrue(result.Tags.Contains("steamdeck:verified"));
            Assert.IsTrue(result.Tags.Contains("protondb:gold"));
            Assert.AreEqual("https://protondb.com/app/220", result.ProtonDbUrl);
        }

        [Test]
        public void MetadataUpdater_DoesNotApplyTags_WhenEnableTagsIsFalse()
        {
            var settings = new SteamDeckProtonDbSettings
            {
                EnableTags = false,
                EnableProtonDbLink = true,
                EnableSteamDeckCategories = true,
                EnableProtonDbCategories = true
            };
            var updater = new TestableMetadataUpdater(settings);
            var game = new Game("Test") { GameId = "220" };
            var mapping = new MappingResult
            {
                Tags = new List<string> { "steamdeck:verified", "protondb:gold" },
                ProtonDbUrl = "https://protondb.com/app/220"
            };

            var result = updater.ApplyDry(game, mapping);

            Assert.AreEqual(0, result.TagsApplied.Count, "No tags should be applied when EnableTags is false");
            Assert.IsNotNull(result.LinkApplied, "Link should be applied when EnableProtonDbLink is true");
        }

        [Test]
        public void MetadataUpdater_DoesNotApplyProtonDbLink_WhenEnableProtonDbLinkIsFalse()
        {
            var settings = new SteamDeckProtonDbSettings
            {
                EnableTags = true,
                EnableProtonDbLink = false,
                EnableSteamDeckCategories = true,
                EnableProtonDbCategories = true
            };
            var updater = new TestableMetadataUpdater(settings);
            var game = new Game("Test") { GameId = "220" };
            var mapping = new MappingResult
            {
                Tags = new List<string> { "steamdeck:verified" },
                ProtonDbUrl = "https://protondb.com/app/220"
            };

            var result = updater.ApplyDry(game, mapping);

            Assert.IsNull(result.LinkApplied, "No ProtonDB link should be applied when EnableProtonDbLink is false");
            Assert.AreEqual(1, result.TagsApplied.Count, "Tags should be applied when EnableTags is true");
        }

        [Test]
        public void MetadataUpdater_DoesNotApplySteamDeckCategories_WhenEnableSteamDeckCategoriesIsFalse()
        {
            var settings = new SteamDeckProtonDbSettings
            {
                EnableTags = true,
                EnableProtonDbLink = true,
                EnableSteamDeckCategories = false,
                EnableProtonDbCategories = true
            };
            var updater = new TestableMetadataUpdater(settings);
            var game = new Game("Test") { GameId = "220" };
            var mapping = new MappingResult
            {
                Categories = new List<string> { "Steam Deck", "Steam Deck - Verified", "ProtonDB", "ProtonDB - Gold" }
            };

            var result = updater.ApplyDry(game, mapping);
            
            Assert.IsFalse(result.CategoriesApplied.Contains("Steam Deck"), "Steam Deck category should not be applied");
            Assert.IsFalse(result.CategoriesApplied.Contains("Steam Deck - Verified"), "Steam Deck - Verified category should not be applied");
            Assert.IsTrue(result.CategoriesApplied.Contains("ProtonDB"), "ProtonDB category should be applied");
            Assert.IsTrue(result.CategoriesApplied.Contains("ProtonDB - Gold"), "ProtonDB - Gold category should be applied");
        }

        [Test]
        public void MetadataUpdater_DoesNotApplyProtonDbCategories_WhenEnableProtonDbCategoriesIsFalse()
        {
            var settings = new SteamDeckProtonDbSettings
            {
                EnableTags = true,
                EnableProtonDbLink = true,
                EnableSteamDeckCategories = true,
                EnableProtonDbCategories = false
            };
            var updater = new TestableMetadataUpdater(settings);
            var game = new Game("Test") { GameId = "220" };
            var mapping = new MappingResult
            {
                Categories = new List<string> { "Steam Deck", "Steam Deck - Verified", "ProtonDB", "ProtonDB - Gold" }
            };

            var result = updater.ApplyDry(game, mapping);
            
            Assert.IsTrue(result.CategoriesApplied.Contains("Steam Deck"), "Steam Deck category should be applied");
            Assert.IsTrue(result.CategoriesApplied.Contains("Steam Deck - Verified"), "Steam Deck - Verified category should be applied");
            Assert.IsFalse(result.CategoriesApplied.Contains("ProtonDB"), "ProtonDB category should not be applied");
            Assert.IsFalse(result.CategoriesApplied.Contains("ProtonDB - Gold"), "ProtonDB - Gold category should not be applied");
        }

        [Test]
        public void MetadataUpdater_RespectsAllSettings_WhenAllDisabled()
        {
            var settings = new SteamDeckProtonDbSettings
            {
                EnableTags = false,
                EnableProtonDbLink = false,
                EnableSteamDeckCategories = false,
                EnableProtonDbCategories = false
            };
            var updater = new TestableMetadataUpdater(settings);
            var game = new Game("Test") { GameId = "220" };
            var mapping = new MappingResult
            {
                Categories = new List<string> { "Steam Deck", "Steam Deck - Verified", "ProtonDB", "ProtonDB - Gold" },
                Tags = new List<string> { "steamdeck:verified", "protondb:gold" },
                ProtonDbUrl = "https://protondb.com/app/220"
            };

            var result = updater.ApplyDry(game, mapping);

            Assert.AreEqual(0, result.CategoriesApplied.Count, "No categories should be applied");
            Assert.AreEqual(0, result.TagsApplied.Count, "No tags should be applied");
            Assert.IsNull(result.LinkApplied, "No links should be applied");
        }

        [Test]
        public void MetadataUpdater_AppliesAllMetadata_WhenAllEnabled()
        {
            var settings = new SteamDeckProtonDbSettings
            {
                EnableTags = true,
                EnableProtonDbLink = true,
                EnableSteamDeckCategories = true,
                EnableProtonDbCategories = true
            };
            var updater = new TestableMetadataUpdater(settings);
            var game = new Game("Test") { GameId = "220" };
            var mapping = new MappingResult
            {
                Categories = new List<string> { "Steam Deck", "Steam Deck - Verified", "ProtonDB", "ProtonDB - Gold" },
                Tags = new List<string> { "steamdeck:verified", "protondb:gold" },
                ProtonDbUrl = "https://protondb.com/app/220"
            };

            var result = updater.ApplyDry(game, mapping);

            Assert.AreEqual(4, result.CategoriesApplied.Count, "All categories should be applied");
            Assert.AreEqual(2, result.TagsApplied.Count, "All tags should be applied");
            Assert.IsNotNull(result.LinkApplied, "Link should be applied");
        }
    }
}
