using System;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http;
using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Polly.Timeout;

namespace SteamDeckProtonDb
{
    /// <summary>
    /// Provides rate limiting and resilience policies for external API calls using Polly.
    /// Implements a custom rate gate plus Polly for retry, circuit breaker, and timeout strategies.
    /// </summary>
    public interface IRateLimiterService
    {
        Task<HttpResponseMessage> ExecuteProtonDbAsync(Func<CancellationToken, Task<HttpResponseMessage>> action, CancellationToken ct = default);
        Task<HttpResponseMessage> ExecuteSteamStoreAsync(Func<CancellationToken, Task<HttpResponseMessage>> action, CancellationToken ct = default);
    }

    /// <summary>
    /// Implements resilience policies for rate limiting and error handling across multiple API services.
    /// Combines a custom rate gate (token bucket style) with Polly's retry, circuit breaker, and timeout.
    /// </summary>
    public class RateLimiterService : IRateLimiterService
    {
        private readonly ResiliencePipeline<HttpResponseMessage> _protonDbPipeline;
        private readonly ResiliencePipeline<HttpResponseMessage> _steamStorePipeline;
        private readonly RateGate _protonDbGate;
        private readonly RateGate _steamStoreGate;
        private static readonly Playnite.SDK.ILogger Logger = Playnite.SDK.LogManager.GetLogger();

        public RateLimiterService(int protonDbRateLimitMs = 1000, int steamStoreRateLimitMs = 600)
        {
            _protonDbPipeline = BuildProtonDbPipeline();
            _steamStorePipeline = BuildSteamStorePipeline();
            // Rate gates: ProtonDB and Steam Store with configurable delays
            _protonDbGate = new RateGate(TimeSpan.FromMilliseconds(protonDbRateLimitMs));
            _steamStoreGate = new RateGate(TimeSpan.FromMilliseconds(steamStoreRateLimitMs));
            Logger.Debug($"Rate limits configured - ProtonDB: {protonDbRateLimitMs}ms (~{60000/protonDbRateLimitMs}/min), Steam Store: {steamStoreRateLimitMs}ms (~{60000/steamStoreRateLimitMs}/min)");
        }

        public async Task<HttpResponseMessage> ExecuteProtonDbAsync(Func<CancellationToken, Task<HttpResponseMessage>> action, CancellationToken ct = default)
        {
            await _protonDbGate.WaitAsync(ct).ConfigureAwait(false);
            return await _protonDbPipeline.ExecuteAsync(async token => await action(token).ConfigureAwait(false), ct).ConfigureAwait(false);
        }

        public async Task<HttpResponseMessage> ExecuteSteamStoreAsync(Func<CancellationToken, Task<HttpResponseMessage>> action, CancellationToken ct = default)
        {
            await _steamStoreGate.WaitAsync(ct).ConfigureAwait(false);
            return await _steamStorePipeline.ExecuteAsync(async token => await action(token).ConfigureAwait(false), ct).ConfigureAwait(false);
        }

        /// <summary>
        /// Builds a resilience pipeline for ProtonDB API with Polly retry, circuit breaker, and timeout.
        /// </summary>
        private static ResiliencePipeline<HttpResponseMessage> BuildProtonDbPipeline()
        {
            var retryOptions = new RetryStrategyOptions<HttpResponseMessage>
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromMilliseconds(200),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .Handle<HttpRequestException>()
                    .Handle<TimeoutRejectedException>()
                    .HandleResult(r =>
                        (int)r.StatusCode >= 500 ||            // 5xx errors
                        (int)r.StatusCode == 429)              // 429 rate limit
            };

            var circuitBreakerOptions = new CircuitBreakerStrategyOptions<HttpResponseMessage>
            {
                FailureRatio = 0.5,
                SamplingDuration = TimeSpan.FromMinutes(1),
                MinimumThroughput = 5,
                BreakDuration = TimeSpan.FromMinutes(2),
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .Handle<HttpRequestException>()
                    .HandleResult(r => (int)r.StatusCode >= 500)
            };

            var timeoutOptions = new TimeoutStrategyOptions
            {
                Timeout = TimeSpan.FromSeconds(10)
            };

            var pipeline = new ResiliencePipelineBuilder<HttpResponseMessage>()
                .AddRetry(retryOptions)
                .AddCircuitBreaker(circuitBreakerOptions)
                .AddTimeout(timeoutOptions)
                .Build();

            Logger.Debug("ProtonDB resilience pipeline built: 3 retries, CB at 50% failure, 10s timeout");
            return pipeline;
        }

        /// <summary>
        /// Builds a resilience pipeline for Steam Store API.
        /// </summary>
        private static ResiliencePipeline<HttpResponseMessage> BuildSteamStorePipeline()
        {
            var retryOptions = new RetryStrategyOptions<HttpResponseMessage>
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromMilliseconds(200),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .Handle<HttpRequestException>()
                    .Handle<TimeoutRejectedException>()
                    .HandleResult(r =>
                        (int)r.StatusCode >= 500 ||
                        (int)r.StatusCode == 429)
            };

            var circuitBreakerOptions = new CircuitBreakerStrategyOptions<HttpResponseMessage>
            {
                FailureRatio = 0.5,
                SamplingDuration = TimeSpan.FromMinutes(1),
                MinimumThroughput = 5,
                BreakDuration = TimeSpan.FromMinutes(2),
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .Handle<HttpRequestException>()
                    .HandleResult(r => (int)r.StatusCode >= 500)
            };

            var timeoutOptions = new TimeoutStrategyOptions
            {
                Timeout = TimeSpan.FromSeconds(10)
            };

            var pipeline = new ResiliencePipelineBuilder<HttpResponseMessage>()
                .AddRetry(retryOptions)
                .AddCircuitBreaker(circuitBreakerOptions)
                .AddTimeout(timeoutOptions)
                .Build();

            Logger.Debug("Steam Store resilience pipeline built: 3 retries, CB at 50% failure, 10s timeout");
            return pipeline;
        }

        /// <summary>
        /// Simple token bucket rate limiter - allows one request every minInterval on average.
        /// </summary>
        private sealed class RateGate
        {
            private readonly SemaphoreSlim _mutex = new SemaphoreSlim(1, 1);
            private readonly TimeSpan _minInterval;
            private DateTime _last = DateTime.MinValue;

            public RateGate(TimeSpan minInterval)
            {
                _minInterval = minInterval <= TimeSpan.Zero ? TimeSpan.Zero : minInterval;
            }

            public async Task WaitAsync(CancellationToken ct)
            {
                await _mutex.WaitAsync(ct).ConfigureAwait(false);
                try
                {
                    var now = DateTime.UtcNow;
                    var next = _last + _minInterval;
                    if (next > now)
                    {
                        var delay = next - now;
                        if (delay > TimeSpan.Zero)
                        {
                            await Task.Delay(delay, ct).ConfigureAwait(false);
                        }
                    }
                    _last = DateTime.UtcNow;
                }
                finally
                {
                    _mutex.Release();
                }
            }
        }
    }
}
