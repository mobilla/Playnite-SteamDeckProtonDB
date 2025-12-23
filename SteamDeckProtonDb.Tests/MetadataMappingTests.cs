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
        public void VerifiedDeck_MapsToCorrectCategoriesAndTags()
        {
            var result = processor.Map(12345, SteamDeckCompatibility.Verified, null);

            Assert.IsNotNull(result);
            Assert.Contains("Steam Deck", result.Categories);
            Assert.Contains("Steam Deck - Verified", result.Categories);
            Assert.Contains("steamdeck:verified", result.Tags);
            Assert.AreEqual(2, result.Categories.Count);
            Assert.AreEqual(1, result.Tags.Count);
        }

        [Test]
        public void PlayableDeck_MapsToCorrectCategoriesAndTags()
        {
            var result = processor.Map(12345, SteamDeckCompatibility.Playable, null);

            Assert.IsNotNull(result);
            Assert.Contains("Steam Deck", result.Categories);
            Assert.Contains("Steam Deck - Playable", result.Categories);
            Assert.Contains("steamdeck:playable", result.Tags);
            Assert.AreEqual(2, result.Categories.Count);
            Assert.AreEqual(1, result.Tags.Count);
        }

        [Test]
        public void UnsupportedDeck_MapsToCorrectCategoriesAndTags()
        {
            var result = processor.Map(12345, SteamDeckCompatibility.Unsupported, null);

            Assert.IsNotNull(result);
            Assert.Contains("Steam Deck", result.Categories);
            Assert.Contains("Steam Deck - Unsupported", result.Categories);
            Assert.Contains("steamdeck:unsupported", result.Tags);
            Assert.AreEqual(2, result.Categories.Count);
            Assert.AreEqual(1, result.Tags.Count);
        }

        [Test]
        public void UnknownDeck_MapsToNoData()
        {
            var result = processor.Map(12345, SteamDeckCompatibility.Unknown, null);

            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Categories.Count);
            Assert.AreEqual(0, result.Tags.Count);
        }

        [Test]
        public void PlatinumProtonDb_MapsToCorrectCategoriesAndTags()
        {
            var protonResult = new ProtonDbResult { Tier = ProtonDbTier.Platinum, Url = "https://protondb.com/app/12345" };
            var result = processor.Map(12345, SteamDeckCompatibility.Unknown, protonResult);

            Assert.IsNotNull(result);
            Assert.Contains("ProtonDB", result.Categories);
            Assert.Contains("ProtonDB - Platinum", result.Categories);
            Assert.Contains("protondb:platinum", result.Tags);
            Assert.AreEqual("https://protondb.com/app/12345", result.ProtonDbUrl);
        }

        [Test]
        public void GoldProtonDb_MapsCorrectly()
        {
            var protonResult = new ProtonDbResult { Tier = ProtonDbTier.Gold, Url = "https://protondb.com/app/999" };
            var result = processor.Map(999, SteamDeckCompatibility.Unknown, protonResult);

            Assert.Contains("ProtonDB", result.Categories);
            Assert.Contains("ProtonDB - Gold", result.Categories);
            Assert.Contains("protondb:gold", result.Tags);
        }

        [Test]
        public void SilverProtonDb_MapsCorrectly()
        {
            var protonResult = new ProtonDbResult { Tier = ProtonDbTier.Silver };
            var result = processor.Map(999, SteamDeckCompatibility.Unknown, protonResult);

            Assert.Contains("ProtonDB - Silver", result.Categories);
            Assert.Contains("protondb:silver", result.Tags);
        }

        [Test]
        public void BronzeProtonDb_MapsCorrectly()
        {
            var protonResult = new ProtonDbResult { Tier = ProtonDbTier.Bronze };
            var result = processor.Map(999, SteamDeckCompatibility.Unknown, protonResult);

            Assert.Contains("ProtonDB - Bronze", result.Categories);
            Assert.Contains("protondb:bronze", result.Tags);
        }

        [Test]
        public void BorkedProtonDb_MapsCorrectly()
        {
            var protonResult = new ProtonDbResult { Tier = ProtonDbTier.Borked };
            var result = processor.Map(999, SteamDeckCompatibility.Unknown, protonResult);

            Assert.Contains("ProtonDB - Borked", result.Categories);
            Assert.Contains("protondb:borked", result.Tags);
        }

        [Test]
        public void CombinedVerifiedAndPlatinum_MapsBothCorrectly()
        {
            var protonResult = new ProtonDbResult { Tier = ProtonDbTier.Platinum, Url = "https://protondb.com/app/123" };
            var result = processor.Map(123, SteamDeckCompatibility.Verified, protonResult);

            // Should have both Steam Deck and ProtonDB categories
            Assert.Contains("Steam Deck", result.Categories);
            Assert.Contains("Steam Deck - Verified", result.Categories);
            Assert.Contains("ProtonDB", result.Categories);
            Assert.Contains("ProtonDB - Platinum", result.Categories);

            // Should have both tags
            Assert.Contains("steamdeck:verified", result.Tags);
            Assert.Contains("protondb:platinum", result.Tags);

            // Should have ProtonDB URL
            Assert.AreEqual("https://protondb.com/app/123", result.ProtonDbUrl);
        }

        [Test]
        public void CombinedPlayableAndGold_MapsBothCorrectly()
        {
            var protonResult = new ProtonDbResult { Tier = ProtonDbTier.Gold, Url = "https://protondb.com/app/456" };
            var result = processor.Map(456, SteamDeckCompatibility.Playable, protonResult);

            Assert.AreEqual(4, result.Categories.Count); // Steam Deck, Steam Deck - Playable, ProtonDB, ProtonDB - Gold
            Assert.AreEqual(2, result.Tags.Count); // steamdeck:playable, protondb:gold
        }

        [Test]
        public void UnknownProtonDb_MapsToNoProtonData()
        {
            var protonResult = new ProtonDbResult { Tier = ProtonDbTier.Unknown };
            var result = processor.Map(123, SteamDeckCompatibility.Unknown, protonResult);

            Assert.AreEqual(0, result.Categories.Count);
            Assert.AreEqual(0, result.Tags.Count);
            Assert.IsNull(result.ProtonDbUrl);
        }

        [Test]
        public void NullProtonDb_MapsToNoProtonData()
        {
            var result = processor.Map(123, SteamDeckCompatibility.Verified, null);

            // Should only have Steam Deck data
            Assert.AreEqual(2, result.Categories.Count);
            Assert.AreEqual(1, result.Tags.Count);
            Assert.Contains("Steam Deck", result.Categories);
            Assert.Contains("steamdeck:verified", result.Tags);
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
        public void DuplicateCategories_NotAdded()
        {
            var result = processor.Map(123, SteamDeckCompatibility.Verified, null);

            // Process again to ensure no duplicates
            var categories = result.Categories.Where(c => c == "Steam Deck").ToList();
            Assert.AreEqual(1, categories.Count, "Should only have one 'Steam Deck' category");
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

            Assert.Contains("Steam Deck - Verified", result.Categories);
        }

        [Test]
        public void NegativeAppId_StillMapsCorrectly()
        {
            var protonResult = new ProtonDbResult { Tier = ProtonDbTier.Gold };
            var result = processor.Map(-1, SteamDeckCompatibility.Unknown, protonResult);

            Assert.Contains("ProtonDB - Gold", result.Categories);
        }
    }
}
