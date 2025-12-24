using NUnit.Framework;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using System;
using SteamDeckProtonDb;
using System.Collections.Generic;

namespace SteamDeckProtonDb.Tests
{
    public class SequencedHandler : HttpMessageHandler
    {
        private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> responses;
        public int CallCount { get; private set; }

        public SequencedHandler(IEnumerable<Func<HttpRequestMessage, HttpResponseMessage>> sequence)
        {
            responses = new Queue<Func<HttpRequestMessage, HttpResponseMessage>>(sequence);
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            CallCount++;
            if (responses.Count == 0)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{}")
                });
            }

            var next = responses.Dequeue();
            return Task.FromResult(next(request));
        }
    }

    [TestFixture]
    public class RateLimitingTests
    {
        [Test]
        public async Task ProtonDbClient_RetriesOn429_ThenSucceeds()
        {
            var json = "{ \"tier\": \"Gold\", \"url\": \"https://protondb.example/app/789\" }";

            var handler = new SequencedHandler(new Func<HttpRequestMessage, HttpResponseMessage>[]
            {
                _ => new HttpResponseMessage((HttpStatusCode)429),
                _ => new HttpResponseMessage((HttpStatusCode)429),
                _ => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(json)
                }
            });

            using (var client = new ProtonDbClient(handler))
            {
                var result = await client.GetGameSummaryAsync(789);
                Assert.IsNotNull(result);
                Assert.AreEqual(ProtonDbTier.Gold, result.Tier);
                Assert.AreEqual("https://protondb.example/app/789", result.Url);
                Assert.AreEqual(3, handler.CallCount, "Expected 2 retries before success");
            }
        }

        [Test]
        public async Task ProtonDbClient_NoRetryOn404_ReturnsUnknown()
        {
            var handler = new SequencedHandler(new Func<HttpRequestMessage, HttpResponseMessage>[]
            {
                _ => new HttpResponseMessage(HttpStatusCode.NotFound)
            });

            using (var client = new ProtonDbClient(handler))
            {
                var result = await client.GetGameSummaryAsync(1010);
                Assert.IsNotNull(result);
                Assert.AreEqual(ProtonDbTier.Unknown, result.Tier);
                Assert.AreEqual(1, handler.CallCount, "Client error should not retry");
            }
        }

        [Test]
        public async Task SteamSource_RetriesOn429_ThenPlayable()
        {
            // New endpoint format with resolved_category = 2 for Playable
            var bodyPlayable = @"{""success"":1,""results"":{""resolved_category"":2}}";

            var handler = new SequencedHandler(new Func<HttpRequestMessage, HttpResponseMessage>[]
            {
                _ => new HttpResponseMessage((HttpStatusCode)429),
                _ => new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(bodyPlayable)
                }
            });

            using (var http = new HttpClient(handler))
            {
                var src = new LocalSteamDeckSource(http);
                var result = await src.GetCompatibilityAsync(2222);
                Assert.AreEqual(SteamDeckCompatibility.Playable, result);
                Assert.AreEqual(2, handler.CallCount, "Expected 1 retry then success");
            }
        }

        [Test]
        public async Task SteamSource_NoRetryOn404_ReturnsUnknown()
        {
            var handler = new SequencedHandler(new Func<HttpRequestMessage, HttpResponseMessage>[]
            {
                _ => new HttpResponseMessage(HttpStatusCode.NotFound)
            });

            using (var http = new HttpClient(handler))
            {
                var src = new LocalSteamDeckSource(http);
                var result = await src.GetCompatibilityAsync(3333);
                Assert.AreEqual(SteamDeckCompatibility.Unknown, result);
                Assert.AreEqual(1, handler.CallCount, "Client error should not retry");
            }
        }
    }
}
