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
            // New endpoint format with resolved_category = 3 for Verified
            var body = @"{""success"":1,""results"":{""resolved_category"":3}}";
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
            // New endpoint format with resolved_category = 2 for Playable
            var body = @"{""success"":1,""results"":{""resolved_category"":2}}";
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
