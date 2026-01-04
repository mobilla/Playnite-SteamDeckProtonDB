# Developer Guide

This guide is for developers who want to build, test, or contribute to the Steam Deck ProtonDB Plugin.

## Table of Contents

- [Building from Source](#building-from-source)
- [Testing](#testing)
- [Architecture](#architecture)
- [API Endpoints](#api-endpoints)
- [Known Limitations](#known-limitations)
- [Contributing](#contributing)

## Building from Source

### Prerequisites

- Visual Studio 2019 or later (or Visual Studio Code with C# extension)
- .NET Framework 4.6.2 SDK
- PowerShell 5.1 or later

### Quick Build

The easiest way to build and test the plugin:

```powershell
cd SteamDeckProtonDb
.\restore-and-build.ps1
```

This script automatically:
- Restores NuGet packages
- Builds the main plugin project
- Verifies XAML settings view is copied correctly
- Builds the test project
- Runs all unit tests
- Reports test results

### Manual Build Steps

If you prefer to build manually:

```powershell
# Restore packages
cd SteamDeckProtonDb
dotnet restore

# Build the plugin
dotnet build

# Build and run tests
cd ../SteamDeckProtonDb.Tests
dotnet build
dotnet run
```

## Testing

The plugin includes comprehensive unit tests covering:
- **ProtonDB JSON parsing**: Valid and malformed API responses
- **Cache manager functionality**: Set, get, expiration, and clear operations
- **Steam Deck API parsing**: Response handling and edge cases
- **Rate limiting**: Pacing and resilience patterns (with test doubles)
- **Settings application**: Configuration validation
- **Metadata mapping**: Category and tag generation

### Run All Tests

```powershell
cd SteamDeckProtonDb.Tests
dotnet run
```

Or use the combined build-and-test script:

```powershell
cd SteamDeckProtonDb
.\restore-and-build.ps1
```

Test results are saved to `TestResult.xml` in the test project directory.

## Architecture

### Core Components

- **SteamDeckProtonDb.cs**: Main plugin entry point, implements `MetadataPlugin`
- **ProtonDbClient.cs**: Handles ProtonDB API calls with rate limiting
- **SteamDeckSource.cs**: Handles Steam Deck compatibility API calls
- **RateLimiterService.cs**: Polly-based rate limiter with circuit breaker
- **CacheManager.cs**: In-memory and file-based caching with TTL
- **SteamDeckProtonDbProvider.cs**: Metadata provider implementation
- **SteamDeckProtonDbSettings.cs**: Plugin settings with validation

### Caching Strategy

The plugin implements dual caching modes:
- **In-Memory Cache**: Fast access for frequently requested games
- **File-Based Cache**: Optional persistent cache across Playnite restarts
- **Default TTL**: 24 hours (1440 minutes)
- **Automatic Invalidation**: Cache entries expire after configured TTL

Cache entries help minimize API calls while keeping data reasonably fresh.

### Rate Limiting

The plugin uses Polly-based rate limiting to respect API limits:

- **ProtonDB**: 1 request per second (1000ms pacing) with 3 retries, 10s timeout, circuit breaker
- **Steam Deck API**: ~100 requests per minute (600ms pacing) with same resilience patterns
- **Retry Strategy**: Exponential backoff (200-800ms) for transient failures (5xx, 429, timeouts)
- **Circuit Breaker**: Opens after 50% failure rate, 2-minute recovery period
- **Timeout**: All operations timeout after 10 seconds to prevent hanging

See [RATE_LIMITING.md](RATE_LIMITING.md) for detailed configuration.

### Error Handling

The plugin gracefully handles errors with comprehensive resilience patterns:
- Network failures fall back to cached data or return `Unknown` status
- Automatic retry with exponential backoff for transient failures
- Circuit breaker protection when services are degraded
- Invalid AppIds are skipped gracefully
- Malformed API responses use fallback parsing strategies
- All operations timeout after 10 seconds to prevent hanging

## API Endpoints

The plugin calls the following public APIs:

### 1. Steam Deck Compatibility API (no API key required)

- **Endpoint**: `https://store.steampowered.com/saleaction/ajaxgetdeckappcompatibilityreport`
- **Rate limit**: ~100 requests per minute (600ms pacing)
- **Resilience**: 3 retries with exponential backoff, circuit breaker, 10s timeout
- **Response format**: JSON with `resolved_category` field
  - `3` = Verified
  - `2` = Playable
  - `1` = Unsupported
  - `0` = Unknown

### 2. ProtonDB API (no API key required)

- **Endpoint**: `https://www.protondb.com/api/v1/reports/summaries/{appid}.json`
- **Rate limit**: 1 request per second (1000ms pacing)
- **Resilience**: 3 retries with exponential backoff, circuit breaker, 10s timeout
- **Response format**: JSON with `tier` field (Platinum, Gold, Silver, Bronze, Borked)

### 3. Steam Store Search API (optional, for non-Steam game matching)

- **Endpoint**: `https://store.steampowered.com/api/storesearch/`
- **Usage**: Attempts to match non-Steam games to Steam AppIDs by title
- **Rate limit**: Same as Steam Deck API (~100 req/min)

Both APIs are public and free to use. The plugin implements comprehensive rate limiting and resilience patterns to be a good API citizen.

## Known Limitations

### Technical Limitations

- The plugin requires a valid Steam AppId to fetch data (not all games have Steam AppIds)
- Metadata fetching is on-demand (not automatic bulk updates)
- Steam Deck compatibility data may lag behind official Steam Store updates
- ProtonDB data depends on community reports and may not reflect the latest game versions or Proton releases

### Performance Considerations

- First fetch for a game takes 1-2 seconds due to API calls
- Cached data returns instantly
- Rate limiting may slow bulk metadata updates (by design)
- Circuit breaker will temporarily block API calls if services are degraded

## Contributing

### Code Style

- Follow standard C# conventions
- Use meaningful variable and method names
- Add XML comments for public APIs
- Keep methods focused and single-purpose

### Testing Guidelines

- Add unit tests for new features
- Test both success and failure paths
- Use test doubles (mocks) for external dependencies
- Ensure all tests pass before submitting PR

### Pull Request Process

1. Fork the repository
2. Create a feature branch (`feature/your-feature-name`)
3. Make your changes with tests
4. Run `.\restore-and-build.ps1` to verify build and tests
5. Submit a pull request with clear description

### Reporting Issues

When reporting bugs, please include:
- Playnite version
- Plugin version
- Steps to reproduce
- Expected vs actual behavior
- Relevant log entries (from Playnite log file)

## Additional Resources

- [RATE_LIMITING.md](RATE_LIMITING.md) - Detailed rate limiting configuration
- [PROGRESS_BAR_INFO.md](PROGRESS_BAR_INFO.md) - Progress UI implementation
- [Playnite SDK Documentation](https://api.playnite.link/)
- [Polly Documentation](https://www.pollydocs.org/)

## License

This plugin is licensed under the MIT License. See the [LICENSE](../LICENSE) file for details.
