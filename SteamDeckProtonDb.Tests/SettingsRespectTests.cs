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

            // Tags gating
            if (settings?.EnableTags == true && result.Tags != null && result.Tags.Count > 0)
            {
                applied.TagsApplied.AddRange(result.Tags);
            }

            // Features gating
            if (settings?.EnableFeatures == true && result.Features != null && result.Features.Count > 0)
            {
                applied.FeaturesApplied.AddRange(result.Features);
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
        public List<string> TagsApplied { get; set; } = new List<string>();
        public List<string> FeaturesApplied { get; set; } = new List<string>();
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
            Assert.IsTrue(result.Tags.Contains("steamdeck:verified"));
            Assert.IsTrue(result.Tags.Contains("protondb:gold"));
            Assert.IsTrue(result.Features.Contains("Steamdeck Verified"));
            Assert.IsTrue(result.Features.Contains("Protondb Gold"));
            Assert.AreEqual("https://protondb.com/app/220", result.ProtonDbUrl);
        }

        [Test]
        public void MetadataUpdater_DoesNotApplyTags_WhenEnableTagsIsFalse()
        {
            var settings = new SteamDeckProtonDbSettings
            {
                EnableTags = false,
                EnableProtonDbLink = true,
                EnableFeatures = true
            };
            var updater = new TestableMetadataUpdater(settings);
            var game = new Game("Test") { GameId = "220" };
            var mapping = new MappingResult
            {
                Tags = new List<string> { "steamdeck:verified", "protondb:gold" },
                Features = new List<string> { "Steamdeck Verified", "Protondb Gold" },
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
                EnableFeatures = true
            };
            var updater = new TestableMetadataUpdater(settings);
            var game = new Game("Test") { GameId = "220" };
            var mapping = new MappingResult
            {
                Tags = new List<string> { "steamdeck:verified" },
                Features = new List<string> { "Steamdeck Verified" },
                ProtonDbUrl = "https://protondb.com/app/220"
            };

            var result = updater.ApplyDry(game, mapping);

            Assert.IsNull(result.LinkApplied, "No ProtonDB link should be applied when EnableProtonDbLink is false");
            Assert.AreEqual(1, result.TagsApplied.Count, "Tags should be applied when EnableTags is true");
        }



        [Test]
        public void MetadataUpdater_RespectsAllSettings_WhenAllDisabled()
        {
            var settings = new SteamDeckProtonDbSettings
            {
                EnableTags = false,
                EnableFeatures = false,
                EnableProtonDbLink = false
            };
            var updater = new TestableMetadataUpdater(settings);
            var game = new Game("Test") { GameId = "220" };
            var mapping = new MappingResult
            {
                Tags = new List<string> { "steamdeck:verified", "protondb:gold" },
                Features = new List<string> { "Steamdeck Verified", "Protondb Gold" },
                ProtonDbUrl = "https://protondb.com/app/220"
            };

            var result = updater.ApplyDry(game, mapping);

            Assert.AreEqual(0, result.TagsApplied.Count, "No tags should be applied");
            Assert.AreEqual(0, result.FeaturesApplied.Count, "No features should be applied");
            Assert.IsNull(result.LinkApplied, "No links should be applied");
        }

        [Test]
        public void MetadataUpdater_AppliesAllMetadata_WhenAllEnabled()
        {
            var settings = new SteamDeckProtonDbSettings
            {
                EnableTags = true,
                EnableFeatures = true,
                EnableProtonDbLink = true
            };
            var updater = new TestableMetadataUpdater(settings);
            var game = new Game("Test") { GameId = "220" };
            var mapping = new MappingResult
            {
                Tags = new List<string> { "steamdeck:verified", "protondb:gold" },
                Features = new List<string> { "Steamdeck Verified", "Protondb Gold" },
                ProtonDbUrl = "https://protondb.com/app/220"
            };

            var result = updater.ApplyDry(game, mapping);

            Assert.AreEqual(2, result.TagsApplied.Count, "All tags should be applied");
            Assert.AreEqual(2, result.FeaturesApplied.Count, "All features should be applied");
            Assert.IsNotNull(result.LinkApplied, "Link should be applied");
        }
    }
}
