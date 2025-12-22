using NUnit.Framework;
using System.Threading.Tasks;
using System;

namespace SteamDeckProtonDb.Tests
{
    public class FakeProtonClient : IProtonDbClient
    {
        public int Calls = 0;
        private readonly ProtonDbResult result;
        public FakeProtonClient(ProtonDbResult result)
        {
            this.result = result;
        }

        public Task<ProtonDbResult> GetGameSummaryAsync(int appId, System.Threading.CancellationToken ct = default)
        {
            Calls++;
            return Task.FromResult(result);
        }
    }

    public class FakeDeckSource : ISteamDeckSource
    {
        public int Calls = 0;
        private readonly SteamDeckCompatibility result;
        public FakeDeckSource(SteamDeckCompatibility result)
        {
            this.result = result;
        }

        public Task<SteamDeckCompatibility> GetCompatibilityAsync(int appId, System.Threading.CancellationToken ct = default)
        {
            Calls++;
            return Task.FromResult(result);
        }
    }

    [TestFixture]
    public class MetadataFetcherTests
    {
        [Test]
        public async Task CachesProtonAndDeckResults()
        {
            var proton = new ProtonDbResult { Tier = ProtonDbTier.Gold, Url = "http://x" };
            var fakeProton = new FakeProtonClient(proton);
            var fakeDeck = new FakeDeckSource(SteamDeckCompatibility.Playable);
            var cache = new InMemoryCacheManager();
            var fetcher = new global::SteamDeckProtonDb.MetadataFetcher(fakeProton, fakeDeck, cache, 1440);

            var r1 = await fetcher.GetBothAsync(100);
            var r2 = await fetcher.GetBothAsync(100);

            Assert.IsNotNull(r1);
            Assert.IsNotNull(r2);
            Assert.AreEqual(1, fakeProton.Calls, "Proton client should be called only once due to cache");
            Assert.AreEqual(1, fakeDeck.Calls, "Deck source should be called only once due to cache");
        }
    }
}
