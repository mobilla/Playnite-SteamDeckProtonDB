using NUnit.Framework;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using System;

namespace SteamDeckProtonDb.Tests
{
    public class SimpleFakeHandler : HttpMessageHandler
    {
        private readonly HttpResponseMessage response;

        public SimpleFakeHandler(HttpResponseMessage response)
        {
            this.response = response;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(response);
        }
    }

    [TestFixture]
    public class LocalSteamDeckSourceTests
    {
        [Test]
        public async Task ParsesVerifiedToken_ReturnsVerified()
        {
            var body = "{ \"some\": \"data\", \"description\": \"This is Steam Deck Verified content\" }";
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body)
            };

            using (var handler = new SimpleFakeHandler(response))
            using (var client = new HttpClient(handler))
            {
                var src = new global::SteamDeckProtonDb.LocalSteamDeckSource(client);
                var result = await src.GetCompatibilityAsync(12345);
                Assert.AreEqual(global::SteamDeckProtonDb.SteamDeckCompatibility.Verified, result);
            }
        }

        [Test]
        public async Task ParsesPlayableToken_ReturnsPlayable()
        {
            var body = "This payload contains playable and other words";
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body)
            };

            using (var handler = new SimpleFakeHandler(response))
            using (var client = new HttpClient(handler))
            {
                var src = new global::SteamDeckProtonDb.LocalSteamDeckSource(client);
                var result = await src.GetCompatibilityAsync(12345);
                Assert.AreEqual(global::SteamDeckProtonDb.SteamDeckCompatibility.Playable, result);
            }
        }
    }
}
