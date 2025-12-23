# Rate Limiting Quick Reference

## For Plugin Developers

### Using the Rate Limiter in New Code

If you need to add rate limiting to a new API client:

```csharp
public class MyNewApiClient
{
    private readonly IRateLimiterService rateLimiter;
    private readonly HttpClient http;

    public MyNewApiClient(HttpClient httpClient)
    {
        http = httpClient;
        rateLimiter = new RateLimiterService();
    }

    public async Task<MyData> FetchDataAsync(string id, CancellationToken ct = default)
    {
        try
        {
            var response = await rateLimiter.ExecuteProtonDbAsync(
                async token => await http.GetAsync($"https://api.example.com/data/{id}", token),
                ct
            );

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return ParseData(content);
            }

            return DefaultValue;
        }
        catch (Polly.CircuitBreaker.BrokenCircuitException)
        {
            logger.Debug("API circuit breaker open - service unavailable");
            return DefaultValue;
        }
        catch (Polly.Retry.RetryExhaustedException ex)
        {
            logger.Debug($"API retries exhausted: {ex.InnerException?.Message}");
            return DefaultValue;
        }
        catch (Polly.TimeoutRejectedException)
        {
            logger.Debug("API request timeout");
            return DefaultValue;
        }
    }
}
```

### Handling Polly Exceptions

Common exceptions you may encounter:

```csharp
try
{
    result = await rateLimiter.ExecuteProtonDbAsync(apiCall, cancellationToken);
}
catch (Polly.CircuitBreaker.BrokenCircuitException)
{
    // Circuit breaker is open - service is experiencing issues
    // Use cached/default value
}
catch (Polly.Retry.RetryExhaustedException ex)
{
    // All retries failed
    // ex.InnerException contains the original error
}
catch (Polly.TimeoutRejectedException)
{
    // Request took too long (10 second timeout)
    // May retry again later
}
catch (OperationCanceledException when ct.IsCancellationRequested)
{
    // User cancelled or timeout triggered
    // Propagate cancellation
    throw;
}
```

## Rate Limit Configurations

### ProtonDB Pipeline
```
Service:     ProtonDB API (https://www.protondb.com/api/v1/reports/summaries/{appId}.json)
Pacing:      Min-interval gate ~1000ms between calls (~60 req/min)
Max Retries: 3 attempts
Backoff:     Exponential: 200ms → 400ms → 800ms + jitter
Timeout:     10 seconds
Circuit:     Opens at 50% failure rate (min 5 requests)
CircuitOpen: 2 minutes recovery time
```

### Steam Store Pipeline
```
Service:     Steam API (https://store.steampowered.com/api/appdetails)
Pacing:      Min-interval gate ~600ms between calls (~100 req/min)
Max Retries: 3 attempts
Backoff:     Exponential: 200ms → 400ms → 800ms + jitter
Timeout:     10 seconds
Circuit:     Opens at 50% failure rate (min 5 requests)
CircuitOpen: 2 minutes recovery time
```

## Monitoring/Debugging

### Enable Debug Logging
The plugin logs rate limiting events. Check Playnite logs:

```
[DEBUG] ProtonDB resilience pipeline built: 3 retries, CB at 50% failure, 10s timeout
[DEBUG] ProtonDB API call for appId 123: https://www.protondb.com/api/v1/reports/summaries/123.json
[DEBUG] ProtonDB API response for appId 123: 200
[DEBUG] Steam Store resilience pipeline built: 3 retries, CB at 50% failure, 10s timeout
[DEBUG] Steam API circuit breaker open - service temporarily unavailable for appId 456
```

### Common Issues

**"circuit breaker open" errors**
- One service is failing repeatedly
- Plugin will retry after 2 minutes
- Check if ProtonDB or Steam is down
- Manual retry available after recovery period

**"retries exhausted" errors**
- Network timeout or service errors
- Plugin tried 3 times and gave up
- Uses cached value if available
- Automatic retry on next API call

**"timeout" errors**
- API took more than 10 seconds to respond
- Transient network issue
- Will retry automatically
- Consider checking network connection

## Integration Points

### FileCacheManager
- Existing 24-hour cache remains primary deduplication
- Rate limiter provides second layer of protection
- Prevents burst requests on cache misses

### Logging
- All rate limiting events logged via Playnite.SDK.LogManager
- Debug level for normal operation
- Check Playnite debug logs for troubleshooting

### Testing
- Mock HTTP handlers work with rate limiter
- Tests use FakeHandler that bypasses rate limits
- Unit tests verify parsing logic, not rate limiting

## Configuration Constants

To adjust limits, edit [SteamDeckProtonDb/RateLimiterService.cs](SteamDeckProtonDb/RateLimiterService.cs):

```
// Pacing gates
_protonDbGate = new RateGate(TimeSpan.FromMilliseconds(1000)); // ProtonDB
_steamStoreGate = new RateGate(TimeSpan.FromMilliseconds(600)); // Steam Store

// Retry Configuration
new RetryStrategyOptions<HttpResponseMessage>
{
    MaxRetryAttempts = 3,
    Delay = TimeSpan.FromMilliseconds(200),
    BackoffType = DelayBackoffType.Exponential,
    UseJitter = true,
}

// Circuit Breaker Configuration
new CircuitBreakerStrategyOptions<HttpResponseMessage>
{
    FailureRatio = 0.5,
    SamplingDuration = TimeSpan.FromMinutes(1),
    MinimumThroughput = 5,
    BreakDuration = TimeSpan.FromMinutes(2),
}

// Timeout Configuration
new TimeoutStrategyOptions
{
    Timeout = TimeSpan.FromSeconds(10)
}
```

## Performance Tips

1. **Use caching**: Your FileCacheManager with 24-hour TTL is the primary rate limiter
2. **Batch requests**: Group related queries to make better use of rate limit quota
3. **Honor 429 responses**: Polly handles these automatically with retries
4. **Test with circuit breaker**: Simulate failures using Polly.Contrib.Simmy for chaos testing
5. **Monitor failure rates**: Log circuit breaker opens to detect service issues early

## References

- Polly GitHub: https://github.com/App-vNext/Polly
- Rate Limiting Article: https://github.com/App-vNext/Polly/wiki/Rate-Limiting
- Resilience Pipelines: https://github.com/App-vNext/Polly/wiki/Resilience-Pipelines
