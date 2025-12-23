using NUnit.Framework;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Net;

namespace SteamDeckProtonDb.Tests
{
    [TestFixture]
    public class SteamDeckApiParsingTests
    {
        [Test]
        public async Task VerifiedKeyword_ReturnsVerified()
        {
            var body = @"{""12345"":{""success"":true,""data"":{""steam_deck"":""Steam Deck Verified""}}}";
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body)
            };

            using (var handler = new SimpleFakeHandler(response))
            using (var client = new HttpClient(handler))
            {
                var src = new LocalSteamDeckSource(client);
                var result = await src.GetCompatibilityAsync(12345);
                Assert.AreEqual(SteamDeckCompatibility.Verified, result);
            }
        }

        [Test]
        public async Task PlayableKeyword_ReturnsPlayable()
        {
            var body = @"{""success"":true,""data"":{""categories"":[{""description"":""Steam Deck Playable""}]}}";
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body)
            };

            using (var handler = new SimpleFakeHandler(response))
            using (var client = new HttpClient(handler))
            {
                var src = new LocalSteamDeckSource(client);
                var result = await src.GetCompatibilityAsync(54321);
                Assert.AreEqual(SteamDeckCompatibility.Playable, result);
            }
        }

        [Test]
        public async Task UnsupportedKeyword_ReturnsUnsupported()
        {
            var body = @"{""data"":{""platform_linux"":false,""steam_deck_note"":""Unsupported""}}";
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body)
            };

            using (var handler = new SimpleFakeHandler(response))
            using (var client = new HttpClient(handler))
            {
                var src = new LocalSteamDeckSource(client);
                var result = await src.GetCompatibilityAsync(99999);
                Assert.AreEqual(SteamDeckCompatibility.Unsupported, result);
            }
        }

        [Test]
        public async Task NoSteamDeckKeywords_ReturnsUnknown()
        {
            var body = @"{""success"":true,""data"":{""name"":""Some Game"",""type"":""game""}}";
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body)
            };

            using (var handler = new SimpleFakeHandler(response))
            using (var client = new HttpClient(handler))
            {
                var src = new LocalSteamDeckSource(client);
                var result = await src.GetCompatibilityAsync(11111);
                Assert.AreEqual(SteamDeckCompatibility.Unknown, result);
            }
        }

        [Test]
        public async Task HttpErrorResponse_ReturnsUnknown()
        {
            var response = new HttpResponseMessage(HttpStatusCode.NotFound);

            using (var handler = new SimpleFakeHandler(response))
            using (var client = new HttpClient(handler))
            {
                var src = new LocalSteamDeckSource(client);
                var result = await src.GetCompatibilityAsync(99999);
                Assert.AreEqual(SteamDeckCompatibility.Unknown, result);
            }
        }

        [Test]
        public async Task InvalidAppId_ReturnsUnknown()
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}")
            };

            using (var handler = new SimpleFakeHandler(response))
            using (var client = new HttpClient(handler))
            {
                var src = new LocalSteamDeckSource(client);
                var result = await src.GetCompatibilityAsync(0);
                Assert.AreEqual(SteamDeckCompatibility.Unknown, result);
            }
        }

        [Test]
        public async Task NegativeAppId_ReturnsUnknown()
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{}")
            };

            using (var handler = new SimpleFakeHandler(response))
            using (var client = new HttpClient(handler))
            {
                var src = new LocalSteamDeckSource(client);
                var result = await src.GetCompatibilityAsync(-123);
                Assert.AreEqual(SteamDeckCompatibility.Unknown, result);
            }
        }

        [Test]
        public async Task EmptyResponse_ReturnsUnknown()
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("")
            };

            using (var handler = new SimpleFakeHandler(response))
            using (var client = new HttpClient(handler))
            {
                var src = new LocalSteamDeckSource(client);
                var result = await src.GetCompatibilityAsync(12345);
                Assert.AreEqual(SteamDeckCompatibility.Unknown, result);
            }
        }

        [Test]
        public async Task MalformedJson_ReturnsUnknown()
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{invalid json")
            };

            using (var handler = new SimpleFakeHandler(response))
            using (var client = new HttpClient(handler))
            {
                var src = new LocalSteamDeckSource(client);
                var result = await src.GetCompatibilityAsync(12345);
                Assert.AreEqual(SteamDeckCompatibility.Unknown, result);
            }
        }

        [Test]
        public async Task CaseInsensitiveMatching_VerifiedLowercase()
        {
            var body = @"{""data"":{""note"":""steam deck verified - works great""}}";
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body)
            };

            using (var handler = new SimpleFakeHandler(response))
            using (var client = new HttpClient(handler))
            {
                var src = new LocalSteamDeckSource(client);
                var result = await src.GetCompatibilityAsync(12345);
                Assert.AreEqual(SteamDeckCompatibility.Verified, result);
            }
        }
    }
}
