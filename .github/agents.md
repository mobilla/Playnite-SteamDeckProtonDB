# Agent Context for Steam Deck & ProtonDB Plugin

## Quick Project Overview
Playnite extension that integrates Steam Deck compatibility and ProtonDB ratings into the Playnite game library manager.

## Key Architecture
- **Main Plugin**: `SteamDeckProtonDb.cs` - Plugin entry point, implements MetadataPlugin
- **API Client**: `ProtonDbClient.cs` - Handles ProtonDB API calls with rate limiting
- **Rate Limiting**: `RateLimiterService.cs` - Polly-based rate limiter (20 req/min, 100 req/hour)
- **Caching**: `CacheManager.cs` - In-memory and disk caching with 7-day TTL
- **Settings**: `SteamDeckProtonDbSettings.cs` + XAML view with runtime loading pattern
- **Provider**: `SteamDeckProtonDbProvider.cs` - Metadata fetching logic

## Critical Patterns

### XAML Settings View
- Uses **runtime XAML loading** (not XAML compilation)
- XAML file must have `CopyToOutputDirectory=PreserveNewest` in .csproj
- Code-behind strips `x:Class` before parsing
- See [.ai-rules.md](../.ai-rules.md) for complete pattern

### Testing
Run: `./SteamDeckProtonDb/restore-and-build.ps1`
- Restores NuGet packages
- Builds main project
- Verifies XAML file copied to output
- Builds and runs all tests

### Dependencies
- Playnite.SDK 6.2.0
- Polly 8.5.0 (rate limiting)
- .NET Framework 4.6.2

## Common Tasks

**Build & Test**: `./SteamDeckProtonDb/restore-and-build.ps1`

**Add new test**: Add to `SteamDeckProtonDb.Tests/`, tests auto-run via `Program.cs`

**Modify settings**: Update XAML + Settings class, rebuild to verify XAML copy

## File Structure
```
SteamDeckProtonDb/          # Main plugin code
SteamDeckProtonDb.Tests/    # All tests
.github/                    # Copilot instructions & agents.md
```

## Avoid These Mistakes
- Don't use MSBuild XAML compilation (use runtime loading)
- Don't call `InitializeComponent()` in settings view
- Don't skip verifying XAML file in bin/Debug after changes
- Don't test rate limiting without mocks (use `TestRateLimiterService`)
