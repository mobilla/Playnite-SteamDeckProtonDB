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
        public async Task VerifiedCategory_ReturnsVerified()
        {
            // New endpoint format: resolved_category = 3 means Verified
            var body = @"{""success"":1,""results"":{""resolved_category"":3}}";
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
        public async Task PlayableCategory_ReturnsPlayable()
        {
            // New endpoint format: resolved_category = 2 means Playable
            var body = @"{""success"":1,""results"":{""resolved_category"":2}}";
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
        public async Task UnsupportedCategory_ReturnsUnsupported()
        {
            // New endpoint format: resolved_category = 1 means Unsupported
            var body = @"{""success"":1,""results"":{""resolved_category"":1}}";
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
        public async Task UnknownCategory_ReturnsUnknown()
        {
            // New endpoint format: resolved_category = 0 means Unknown
            var body = @"{""success"":1,""results"":{""resolved_category"":0}}";
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
        public async Task NoResultsInResponse_ReturnsUnknown()
        {
            // When success=1 but no results, means the game hasn't been tested
            var body = @"{""success"":1}";
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body)
            };

            using (var handler = new SimpleFakeHandler(response))
            using (var client = new HttpClient(handler))
            {
                var src = new LocalSteamDeckSource(client);
                var result = await src.GetCompatibilityAsync(22222);
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
        public async Task InvalidCategoryValue_ReturnsUnknown()
        {
            // If the resolved_category has an unexpected value, should return Unknown
            var body = @"{""success"":1,""results"":{""resolved_category"":999}}";
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body)
            };

            using (var handler = new SimpleFakeHandler(response))
            using (var client = new HttpClient(handler))
            {
                var src = new LocalSteamDeckSource(client);
                var result = await src.GetCompatibilityAsync(12345);
                Assert.AreEqual(SteamDeckCompatibility.Unknown, result);
            }
        }
    }
}
