using NUnit.Framework;
using Playnite.SDK.Models;
using System;
using System.Collections.Generic;

namespace SteamDeckProtonDb.Tests
{
    [TestFixture]
    public class MetadataUpdaterCleanupTests
    {
        [Test]
        public void RemovePluginTags_RemovesOutdatedPluginTags()
        {
            var settings = new SteamDeckProtonDbSettings();
            var keepTag = new Tag { Id = Guid.NewGuid(), Name = "favorite" };
            var oldDeckTag = new Tag { Id = Guid.NewGuid(), Name = "steamdeck:verified" };
            var oldProtonTag = new Tag { Id = Guid.NewGuid(), Name = "protondb:gold" };
            var tagPool = new List<Tag> { keepTag, oldDeckTag, oldProtonTag };

            var game = new Game("Test Game")
            {
                TagIds = new List<Guid> { keepTag.Id, oldDeckTag.Id, oldProtonTag.Id }
            };

            MetadataUpdaterHelpers.RemovePluginTags(game, tagPool, new[] { "steamdeck:playable" }, settings);

            CollectionAssert.AreEquivalent(new[] { keepTag.Id }, game.TagIds);
        }

        [Test]
        public void RemovePluginTags_KeepsDesiredPluginTags()
        {
            var settings = new SteamDeckProtonDbSettings();
            var keepTag = new Tag { Id = Guid.NewGuid(), Name = "favorite" };
            var desiredProtonTag = new Tag { Id = Guid.NewGuid(), Name = "protondb:silver" };
            var oldDeckTag = new Tag { Id = Guid.NewGuid(), Name = "steamdeck:verified" };
            var tagPool = new List<Tag> { keepTag, desiredProtonTag, oldDeckTag };

            var game = new Game("Test Game")
            {
                TagIds = new List<Guid> { keepTag.Id, desiredProtonTag.Id, oldDeckTag.Id }
            };

            MetadataUpdaterHelpers.RemovePluginTags(game, tagPool, new[] { "protondb:silver" }, settings);

            CollectionAssert.AreEquivalent(new[] { keepTag.Id, desiredProtonTag.Id }, game.TagIds);
        }

        [Test]
        public void RemovePluginFeatures_RemovesOutdatedPluginFeatures()
        {
            var settings = new SteamDeckProtonDbSettings();
            var keepFeature = new GameFeature { Id = Guid.NewGuid(), Name = "Singleplayer" };
            var oldDeckFeature = new GameFeature { Id = Guid.NewGuid(), Name = "Steamdeck Verified" };
            var oldProtonFeature = new GameFeature { Id = Guid.NewGuid(), Name = "Protondb Gold" };
            var featurePool = new List<GameFeature> { keepFeature, oldDeckFeature, oldProtonFeature };

            var game = new Game("Test Game")
            {
                FeatureIds = new List<Guid> { keepFeature.Id, oldDeckFeature.Id, oldProtonFeature.Id }
            };

            MetadataUpdaterHelpers.RemovePluginFeatures(game, featurePool, new[] { "Protondb Silver" }, settings);

            CollectionAssert.AreEquivalent(new[] { keepFeature.Id }, game.FeatureIds);
        }

        [Test]
        public void RemovePluginFeatures_KeepsDesiredPluginFeatures()
        {
            var settings = new SteamDeckProtonDbSettings();
            var keepFeature = new GameFeature { Id = Guid.NewGuid(), Name = "Singleplayer" };
            var desiredDeckFeature = new GameFeature { Id = Guid.NewGuid(), Name = "Steamdeck Playable" };
            var oldDeckFeature = new GameFeature { Id = Guid.NewGuid(), Name = "Steamdeck Unsupported" };
            var featurePool = new List<GameFeature> { keepFeature, desiredDeckFeature, oldDeckFeature };

            var game = new Game("Test Game")
            {
                FeatureIds = new List<Guid> { keepFeature.Id, desiredDeckFeature.Id, oldDeckFeature.Id }
            };

            MetadataUpdaterHelpers.RemovePluginFeatures(game, featurePool, new[] { "Steamdeck Playable" }, settings);

            CollectionAssert.AreEquivalent(new[] { keepFeature.Id, desiredDeckFeature.Id }, game.FeatureIds);
        }
    }
}
