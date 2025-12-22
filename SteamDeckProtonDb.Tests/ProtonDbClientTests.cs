using NUnit.Framework;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using System;
using SteamDeckProtonDb;

namespace SteamDeckProtonDb.Tests
{
    public class FakeHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage response;

        public FakeHandler(HttpResponseMessage response)
        {
            this.response = response;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(response);
        }
    }

    [TestFixture]
    public class ProtonDbClientTests
    {
        [Test]
        public async Task ParsesValidJson_ReturnsTierAndUrl()
        {
            var json = "{ \"tier\": \"Platinum\", \"url\": \"https://protondb.example/app/123\" }";
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json)
            };

            using (var handler = new FakeHandler(response))
            using (var client = new ProtonDbClient(handler))
            {
                var result = await client.GetGameSummaryAsync(123);

                Assert.IsNotNull(result);
                Assert.AreEqual(ProtonDbTier.Platinum, result.Tier);
                Assert.AreEqual("https://protondb.example/app/123", result.Url);
                return;
            }
        }

        [Test]
        public async Task MalformedJson_ReturnsUnknownTierAndFallbackUrl()
        {
            var json = "this is not json";
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json)
            };

            using (var handler = new FakeHandler(response))
            using (var client = new ProtonDbClient(handler))
            {
                var appId = 456;
                var result = await client.GetGameSummaryAsync(appId);

                Assert.IsNotNull(result);
                Assert.AreEqual(ProtonDbTier.Unknown, result.Tier);
                Assert.AreEqual($"https://www.protondb.com/app/{appId}", result.Url);
                return;
            }
        }
    }
}
