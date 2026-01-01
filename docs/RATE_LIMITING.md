# Rate Limiting Guide

## TL;DR
- **ProtonDB**: 1 req/sec with 3 retries, 10s timeout, circuit breaker
- **Steam Store**: ~100 req/min with same resilience patterns
- **Usage**: Wrap API calls in `rateLimiter.ExecuteProtonDbAsync()` or `ExecuteSteamStoreAsync()`
- **Config**: Edit `RateLimiterService.cs` to adjust limits

## Usage Example

```csharp
public async Task<MyData> FetchDataAsync(string id, CancellationToken ct = default)
{
    try
    {
        var response = await rateLimiter.ExecuteProtonDbAsync(
            async token => await http.GetAsync($"https://api.example.com/{id}", token), ct);
        
        return response.IsSuccessStatusCode ? ParseData(await response.Content.ReadAsStringAsync()) : DefaultValue;
    }
    catch (Polly.CircuitBreaker.BrokenCircuitException) { return DefaultValue; } // Service down
    catch (Polly.Retry.RetryExhaustedException) { return DefaultValue; }         // All retries failed
    catch (Polly.TimeoutRejectedException) { return DefaultValue; }              // 10s timeout
}
```

## Rate Limits

| Service | Pacing | Retries | Timeout | Circuit Breaker |
|---------|--------|---------|---------|-----------------|
| ProtonDB | 1000ms (~60/min) | 3x exponential (200-800ms) | 10s | 50% failure, 2min recovery |
| Steam Store | 600ms (~100/min) | 3x exponential (200-800ms) | 10s | 50% failure, 2min recovery |

## Implementation Details

### Architecture
- **RateLimiterService.cs**: Centralized resilience/pacing service
- **Custom rate gate**: Lightweight min-interval pacing (no Polly.RateLimiting dependency)
- **Polly pipelines**: Retry + Circuit Breaker + Timeout per service
- **Integration**: ProtonDbClient and SteamDeckSource use the service

### How It Works

1. **Pacing Gate** (Min-Interval Token):
   - Lightweight min-interval gate per service
   - ProtonDB: wait up to 1000ms since last call; Steam: 600ms
   - Prevents bursts without additional dependencies

2. **Retry Strategy** (Exponential Backoff):
   - Retries on transient failures (5xx, 429, timeouts)
   Architecture

- **RateLimiterService.cs**: Centralized Polly pipelines (Retry + Circuit Breaker + Timeout)
- **Pacing**: Custom min-interval gates (1000ms ProtonDB, 600ms Steam)
- **Integration**: Works with existing FileCacheManager (24h TTL) and mocked HTTP handlers
- **Error handling**: Retries transient failures (5xx, 429), fails fast on 4xx
**"timeout" errors**
- API took more than 10 seconds
- Transient network issue
- Will retry automatically
- Consider checking network connection

### Debug Logging

Check Playnite logs for rate limiting events:

```
[DEBUG] ProtonDB resilience pipeline built: 3 retries, CB at 50% failure, 10s timeout
[DEBUG] ProtonDB API call for appId 123: https://www.protondb.com/api/v1/reports/summaries/123.json
[DEBUG] ProtonDB API response for appId 123: 200
[DEBUG] Steam Store resilience pipeline built: 3 retries, CB at 50% failure, 10s timeout
[DEBUG] Steam API circuit breaker open - service temporarily unavailable for appId 456
```

## Configuration

Edit [RateLimiterService.cs](../SteamDeckProtonDb/RateLimiterService.cs) to adjust:
- Pacing gates: `FromMilliseconds(1000)` or `(600)` 
- Retries: `MaxRetryAttempts = 3`, `Delay = FromMilliseconds(200)`
- Circuit breaker: `FailureRatio = 0.5`, `BreakDuration = FromMinutes(2)`
- Timeout: `Timeout = FromSeconds(10)`

**References**: [Polly GitHub](https://github.com/App-vNext/Polly) | [Circuit Breaker](https://github.com/App-vNext/Polly/wiki/Circuit-Breaker) |