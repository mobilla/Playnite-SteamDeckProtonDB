using NUnit.Framework;
using Playnite.SDK.Models;
using System.Collections.Generic;

namespace SteamDeckProtonDb.Tests
{
    [TestFixture]
    public class SettingsApplicationTests
    {
        [Test]
        public void AllSettingsEnabled_AllMetadataApplied()
        {
            var settings = new SteamDeckProtonDbSettings(null)
            {
                EnableSteamDeckCategories = true,
                EnableProtonDbCategories = true,
                EnableTags = true,
                EnableProtonDbLink = true
            };

            var testUpdater = new TestableMetadataUpdater(settings);
            var game = new Game("Test Game");

            var mappingResult = new MappingResult
            {
                Categories = new List<string> { "Steam Deck - Verified", "ProtonDB - Platinum" },
                Tags = new List<string> { "steamdeck:verified", "protondb:platinum" },
                ProtonDbUrl = "https://protondb.com/app/123"
            };

            var result = testUpdater.ApplyDry(game, mappingResult);

            Assert.AreEqual(2, result.CategoriesApplied.Count);
            Assert.AreEqual(2, result.TagsApplied.Count);
            Assert.IsNotNull(result.LinkApplied);
        }

        [Test]
        public void SteamDeckCategoriesDisabled_SteamDeckNotAdded()
        {
            var settings = new SteamDeckProtonDbSettings(null)
            {
                EnableSteamDeckCategories = false,
                EnableProtonDbCategories = true,
                EnableTags = true
            };

            var testUpdater = new TestableMetadataUpdater(settings);
            var game = new Game("Test Game");

            var mappingResult = new MappingResult
            {
                Categories = new List<string> { "Steam Deck - Verified", "ProtonDB - Gold" },
                Tags = new List<string> { "steamdeck:verified", "protondb:gold" }
            };

            var result = testUpdater.ApplyDry(game, mappingResult);

            Assert.AreEqual(1, result.CategoriesApplied.Count, "Should only add ProtonDB category");
            Assert.Contains("ProtonDB - Gold", result.CategoriesApplied);
            Assert.IsFalse(result.CategoriesApplied.Contains("Steam Deck - Verified"));
        }

        [Test]
        public void ProtonDbCategoriesDisabled_ProtonDbNotAdded()
        {
            var settings = new SteamDeckProtonDbSettings(null)
            {
                EnableSteamDeckCategories = true,
                EnableProtonDbCategories = false,
                EnableTags = true
            };

            var testUpdater = new TestableMetadataUpdater(settings);
            var game = new Game("Test Game");

            var mappingResult = new MappingResult
            {
                Categories = new List<string> { "Steam Deck - Playable", "ProtonDB - Silver" },
                Tags = new List<string> { "steamdeck:playable", "protondb:silver" }
            };

            var result = testUpdater.ApplyDry(game, mappingResult);

            Assert.AreEqual(1, result.CategoriesApplied.Count, "Should only add Steam Deck category");
            Assert.Contains("Steam Deck - Playable", result.CategoriesApplied);
            Assert.IsFalse(result.CategoriesApplied.Contains("ProtonDB - Silver"));
        }

        [Test]
        public void TagsDisabled_NoTagsAdded()
        {
            var settings = new SteamDeckProtonDbSettings(null)
            {
                EnableSteamDeckCategories = true,
                EnableProtonDbCategories = true,
                EnableTags = false
            };

            var testUpdater = new TestableMetadataUpdater(settings);
            var game = new Game("Test Game");

            var mappingResult = new MappingResult
            {
                Categories = new List<string> { "Steam Deck - Verified" },
                Tags = new List<string> { "steamdeck:verified", "protondb:platinum" }
            };

            var result = testUpdater.ApplyDry(game, mappingResult);

            Assert.AreEqual(0, result.TagsApplied.Count, "No tags should be added");
            Assert.Greater(result.CategoriesApplied.Count, 0, "Categories should still be added");
        }

        [Test]
        public void ProtonDbLinkDisabled_NoLinkAdded()
        {
            var settings = new SteamDeckProtonDbSettings(null)
            {
                EnableSteamDeckCategories = true,
                EnableProtonDbCategories = true,
                EnableTags = true,
                EnableProtonDbLink = false
            };

            var testUpdater = new TestableMetadataUpdater(settings);
            var game = new Game("Test Game");

            var mappingResult = new MappingResult
            {
                Categories = new List<string> { "ProtonDB - Gold" },
                Tags = new List<string> { "protondb:gold" },
                ProtonDbUrl = "https://protondb.com/app/456"
            };

            var result = testUpdater.ApplyDry(game, mappingResult);

            Assert.IsNull(result.LinkApplied, "Link should not be added");
            Assert.Greater(result.CategoriesApplied.Count, 0, "Categories should still be added");
        }

        [Test]
        public void AllSettingsDisabled_NoMetadataApplied()
        {
            var settings = new SteamDeckProtonDbSettings(null)
            {
                EnableSteamDeckCategories = false,
                EnableProtonDbCategories = false,
                EnableTags = false,
                EnableProtonDbLink = false
            };

            var testUpdater = new TestableMetadataUpdater(settings);
            var game = new Game("Test Game");

            var mappingResult = new MappingResult
            {
                Categories = new List<string> { "Steam Deck - Verified", "ProtonDB - Platinum" },
                Tags = new List<string> { "steamdeck:verified", "protondb:platinum" },
                ProtonDbUrl = "https://protondb.com/app/789"
            };

            var result = testUpdater.ApplyDry(game, mappingResult);

            Assert.AreEqual(0, result.CategoriesApplied.Count);
            Assert.AreEqual(0, result.TagsApplied.Count);
            Assert.IsNull(result.LinkApplied);
        }

        [Test]
        public void NullGame_NoExceptionThrown()
        {
            var settings = new SteamDeckProtonDbSettings(null)
            {
                EnableSteamDeckCategories = true
            };

            var testUpdater = new TestableMetadataUpdater(settings);
            var mappingResult = new MappingResult
            {
                Categories = new List<string> { "Steam Deck - Verified" }
            };

            var result = testUpdater.ApplyDry(null, mappingResult);

            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.CategoriesApplied.Count);
        }

        [Test]
        public void NullMappingResult_NoExceptionThrown()
        {
            var settings = new SteamDeckProtonDbSettings(null)
            {
                EnableSteamDeckCategories = true
            };

            var testUpdater = new TestableMetadataUpdater(settings);
            var game = new Game("Test Game");

            var result = testUpdater.ApplyDry(game, null);

            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.CategoriesApplied.Count);
        }

        [Test]
        public void EmptyCategories_HandlesGracefully()
        {
            var settings = new SteamDeckProtonDbSettings(null)
            {
                EnableSteamDeckCategories = true,
                EnableProtonDbCategories = true
            };

            var testUpdater = new TestableMetadataUpdater(settings);
            var game = new Game("Test Game");

            var mappingResult = new MappingResult
            {
                Categories = new List<string>(),
                Tags = new List<string> { "some:tag" }
            };

            var result = testUpdater.ApplyDry(game, mappingResult);

            Assert.AreEqual(0, result.CategoriesApplied.Count);
        }

        [Test]
        public void SteamDeckParentCategory_IncludedWhenDeckEnabled()
        {
            var settings = new SteamDeckProtonDbSettings(null)
            {
                EnableSteamDeckCategories = true,
                EnableProtonDbCategories = false
            };

            var testUpdater = new TestableMetadataUpdater(settings);
            var game = new Game("Test Game");

            var mappingResult = new MappingResult
            {
                Categories = new List<string> { "Steam Deck", "Steam Deck - Verified" }
            };

            var result = testUpdater.ApplyDry(game, mappingResult);

            Assert.AreEqual(2, result.CategoriesApplied.Count);
            Assert.Contains("Steam Deck", result.CategoriesApplied);
            Assert.Contains("Steam Deck - Verified", result.CategoriesApplied);
        }

        [Test]
        public void ProtonDbParentCategory_IncludedWhenProtonEnabled()
        {
            var settings = new SteamDeckProtonDbSettings(null)
            {
                EnableSteamDeckCategories = false,
                EnableProtonDbCategories = true
            };

            var testUpdater = new TestableMetadataUpdater(settings);
            var game = new Game("Test Game");

            var mappingResult = new MappingResult
            {
                Categories = new List<string> { "ProtonDB", "ProtonDB - Gold" }
            };

            var result = testUpdater.ApplyDry(game, mappingResult);

            Assert.AreEqual(2, result.CategoriesApplied.Count);
            Assert.Contains("ProtonDB", result.CategoriesApplied);
            Assert.Contains("ProtonDB - Gold", result.CategoriesApplied);
        }

        [Test]
        public void MixedSettings_OnlyEnabledMetadataApplied()
        {
            var settings = new SteamDeckProtonDbSettings(null)
            {
                EnableSteamDeckCategories = true,
                EnableProtonDbCategories = false,
                EnableTags = false,
                EnableProtonDbLink = true
            };

            var testUpdater = new TestableMetadataUpdater(settings);
            var game = new Game("Test Game");

            var mappingResult = new MappingResult
            {
                Categories = new List<string> { "Steam Deck - Playable", "ProtonDB - Bronze" },
                Tags = new List<string> { "steamdeck:playable", "protondb:bronze" },
                ProtonDbUrl = "https://protondb.com/app/111"
            };

            var result = testUpdater.ApplyDry(game, mappingResult);

            Assert.AreEqual(1, result.CategoriesApplied.Count, "Only Steam Deck category");
            Assert.Contains("Steam Deck - Playable", result.CategoriesApplied);
            Assert.AreEqual(0, result.TagsApplied.Count, "Tags disabled");
            Assert.IsNotNull(result.LinkApplied, "Link enabled");
        }

        [Test]
        public void EmptyProtonDbUrl_NoLinkAdded()
        {
            var settings = new SteamDeckProtonDbSettings(null)
            {
                EnableProtonDbLink = true
            };

            var testUpdater = new TestableMetadataUpdater(settings);
            var game = new Game("Test Game");

            var mappingResult = new MappingResult
            {
                ProtonDbUrl = ""
            };

            var result = testUpdater.ApplyDry(game, mappingResult);

            Assert.IsNull(result.LinkApplied);
        }

        [Test]
        public void NullProtonDbUrl_NoLinkAdded()
        {
            var settings = new SteamDeckProtonDbSettings(null)
            {
                EnableProtonDbLink = true
            };

            var testUpdater = new TestableMetadataUpdater(settings);
            var game = new Game("Test Game");

            var mappingResult = new MappingResult
            {
                ProtonDbUrl = null
            };

            var result = testUpdater.ApplyDry(game, mappingResult);

            Assert.IsNull(result.LinkApplied);
        }
    }
}
