using NUnit.Framework;
using System.Linq;

namespace SteamDeckProtonDb.Tests
{
    [TestFixture]
    public class MetadataMappingTests
    {
        private MetadataProcessor processor;

        [SetUp]
        public void Setup()
        {
            processor = new MetadataProcessor();
        }

        [Test]
        public void VerifiedDeck_MapsToCorrectTagsAndFeatures()
        {
            var result = processor.Map(12345, SteamDeckCompatibility.Verified, null);

            Assert.IsNotNull(result);
            Assert.Contains("steamdeck:verified", result.Tags);
            Assert.Contains("Steamdeck Verified", result.Features);
            Assert.AreEqual(1, result.Tags.Count);
            Assert.AreEqual(1, result.Features.Count);
        }

        [Test]
        public void PlayableDeck_MapsToCorrectTagsAndFeatures()
        {
            var result = processor.Map(12345, SteamDeckCompatibility.Playable, null);

            Assert.IsNotNull(result);
            Assert.Contains("steamdeck:playable", result.Tags);
            Assert.Contains("Steamdeck Playable", result.Features);
            Assert.AreEqual(1, result.Tags.Count);
            Assert.AreEqual(1, result.Features.Count);
        }

        [Test]
        public void UnsupportedDeck_MapsToCorrectTagsAndFeatures()
        {
            var result = processor.Map(12345, SteamDeckCompatibility.Unsupported, null);

            Assert.IsNotNull(result);
            Assert.Contains("steamdeck:unsupported", result.Tags);
            Assert.Contains("Steamdeck Unsupported", result.Features);
            Assert.AreEqual(1, result.Tags.Count);
            Assert.AreEqual(1, result.Features.Count);
        }

        [Test]
        public void UnknownDeck_MapsToNoData()
        {
            var result = processor.Map(12345, SteamDeckCompatibility.Unknown, null);

            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Tags.Count);
            Assert.AreEqual(0, result.Features.Count);
        }

        [Test]
        public void PlatinumProtonDb_MapsToCorrectTagsAndFeatures()
        {
            var protonResult = new ProtonDbResult { Tier = ProtonDbTier.Platinum, Url = "https://protondb.com/app/12345" };
            var result = processor.Map(12345, SteamDeckCompatibility.Unknown, protonResult);

            Assert.IsNotNull(result);
            Assert.Contains("protondb:platinum", result.Tags);
            Assert.Contains("Protondb Platinum", result.Features);
            Assert.AreEqual("https://protondb.com/app/12345", result.ProtonDbUrl);
        }

        [Test]
        public void GoldProtonDb_MapsCorrectly()
        {
            var protonResult = new ProtonDbResult { Tier = ProtonDbTier.Gold, Url = "https://protondb.com/app/999" };
            var result = processor.Map(999, SteamDeckCompatibility.Unknown, protonResult);

            Assert.Contains("protondb:gold", result.Tags);
            Assert.Contains("Protondb Gold", result.Features);
        }

        [Test]
        public void SilverProtonDb_MapsCorrectly()
        {
            var protonResult = new ProtonDbResult { Tier = ProtonDbTier.Silver };
            var result = processor.Map(999, SteamDeckCompatibility.Unknown, protonResult);

            Assert.Contains("protondb:silver", result.Tags);
            Assert.Contains("Protondb Silver", result.Features);
        }

        [Test]
        public void BronzeProtonDb_MapsCorrectly()
        {
            var protonResult = new ProtonDbResult { Tier = ProtonDbTier.Bronze };
            var result = processor.Map(999, SteamDeckCompatibility.Unknown, protonResult);

            Assert.Contains("protondb:bronze", result.Tags);
            Assert.Contains("Protondb Bronze", result.Features);
        }

        [Test]
        public void BorkedProtonDb_MapsCorrectly()
        {
            var protonResult = new ProtonDbResult { Tier = ProtonDbTier.Borked };
            var result = processor.Map(999, SteamDeckCompatibility.Unknown, protonResult);

            Assert.Contains("protondb:borked", result.Tags);
            Assert.Contains("Protondb Borked", result.Features);
        }

        [Test]
        public void CombinedVerifiedAndPlatinum_MapsBothCorrectly()
        {
            var protonResult = new ProtonDbResult { Tier = ProtonDbTier.Platinum, Url = "https://protondb.com/app/123" };
            var result = processor.Map(123, SteamDeckCompatibility.Verified, protonResult);

            // Should have both Steam Deck and ProtonDB tags/features
            Assert.Contains("steamdeck:verified", result.Tags);
            Assert.Contains("protondb:platinum", result.Tags);

            // Should have both features
            Assert.Contains("Steamdeck Verified", result.Features);
            Assert.Contains("Protondb Platinum", result.Features);

            // Should have ProtonDB URL
            Assert.AreEqual("https://protondb.com/app/123", result.ProtonDbUrl);
        }

        [Test]
        public void CombinedPlayableAndGold_MapsBothCorrectly()
        {
            var protonResult = new ProtonDbResult { Tier = ProtonDbTier.Gold, Url = "https://protondb.com/app/456" };
            var result = processor.Map(456, SteamDeckCompatibility.Playable, protonResult);

            Assert.AreEqual(2, result.Tags.Count); // steamdeck:playable, protondb:gold
            Assert.AreEqual(2, result.Features.Count); // Steamdeck Playable, Protondb Gold
        }

        [Test]
        public void UnknownProtonDb_MapsToNoProtonData()
        {
            var protonResult = new ProtonDbResult { Tier = ProtonDbTier.Unknown };
            var result = processor.Map(123, SteamDeckCompatibility.Unknown, protonResult);

            Assert.AreEqual(0, result.Tags.Count);
            Assert.AreEqual(0, result.Features.Count);
            Assert.IsNull(result.ProtonDbUrl);
        }

        [Test]
        public void NullProtonDb_MapsToNoProtonData()
        {
            var result = processor.Map(123, SteamDeckCompatibility.Verified, null);

            // Should only have Steam Deck data
            Assert.AreEqual(1, result.Tags.Count);
            Assert.AreEqual(1, result.Features.Count);
            Assert.Contains("steamdeck:verified", result.Tags);
            Assert.Contains("Steamdeck Verified", result.Features);
        }

        [Test]
        public void ProtonDbUrlHandling_PreservesUrl()
        {
            var protonResult = new ProtonDbResult 
            { 
                Tier = ProtonDbTier.Gold, 
                Url = "https://protondb.com/app/12345?custom=param" 
            };
            var result = processor.Map(12345, SteamDeckCompatibility.Unknown, protonResult);

            Assert.AreEqual("https://protondb.com/app/12345?custom=param", result.ProtonDbUrl);
        }

        [Test]
        public void DuplicateTags_NotAdded()
        {
            var result = processor.Map(123, SteamDeckCompatibility.Verified, null);

            // Verify tags list has no duplicates
            var verifiedTags = result.Tags.Where(t => t == "steamdeck:verified").ToList();
            Assert.AreEqual(1, verifiedTags.Count, "Should only have one 'steamdeck:verified' tag");
        }

        [Test]
        public void TagsAreLowercase_ProtonDbTiers()
        {
            var protonResult = new ProtonDbResult { Tier = ProtonDbTier.Platinum };
            var result = processor.Map(123, SteamDeckCompatibility.Unknown, protonResult);

            Assert.Contains("protondb:platinum", result.Tags);
            Assert.IsFalse(result.Tags.Any(t => t == "protondb:Platinum"));
        }

        [Test]
        public void ZeroAppId_StillMapsCorrectly()
        {
            var result = processor.Map(0, SteamDeckCompatibility.Verified, null);

            Assert.Contains("steamdeck:verified", result.Tags);
        }

        [Test]
        public void NegativeAppId_StillMapsCorrectly()
        {
            var protonResult = new ProtonDbResult { Tier = ProtonDbTier.Gold };
            var result = processor.Map(-1, SteamDeckCompatibility.Unknown, protonResult);

            Assert.Contains("protondb:gold", result.Tags);
        }
    }
}
