# Playnite Plugin Plan: Steam Deck and ProtonDB Metadata Integration

## Overview

This document expands the original plan into a concrete implementation roadmap for a Playnite metadata plugin that: (1) obtains Steam Deck compatibility information and (2) fetches ProtonDB community tier data, then maps both to Playnite metadata (categories, tags, links, or features). The plugin targets C# / .NET Framework 4.6.2 and will be structured for testability, caching, and safe rate-limited API usage.

## Components (Detailed)

- **MetadataFetcher**: Responsible for all network and local data retrieval. Implements pluggable adapters:
   - `IProtonDbClient` — fetches ProtonDB game summary by Steam AppId.
   - `ISteamDeckSource` — returns Steam Deck compatibility from either local Steam metadata files or Steam Web API.
   - Includes retries, exponential backoff, and circuit-breaker style short-circuiting on repeated failures.

- **MetadataProcessor**: Normalizes external data into internal types and mapping results:
   - `SteamDeckCompatibility` enum: `Verified`, `Playable`, `Unsupported`, `Unknown`.
   - `ProtonDbTier` enum: `Platinum`, `Gold`, `Silver`, `Bronze`, `Plausible`, `Borked`, `Unknown`.
   - Validation and fallback logic when APIs return incomplete or unexpected payloads.

- **MetadataMapper**: Converts normalized results into Playnite metadata changes according to user settings:
   - Category mapping (add/remove), tag mapping, and optional feature updates.
   - Link generation: adds ProtonDB summary link to game's links list (configurable per-user preference).

- **MetadataUpdater**: Uses Playnite SDK to apply metadata updates. Supports a `dry-run` mode and batching for minimal I/O.

- **Settings & UI**: `SteamDeckProtonDbSettings` and `SteamDeckProtonDbSettingsView.xaml` to expose:
   - Toggles: update categories, add tags, add links, update features.
   - ProtonDB API key (optional if ProtonDB enables key use), cache TTL, request rate limit, dry-run option.

- **Caching & Persistence**: Local cache with TTL (file-backed JSON in plugin data folder) and optional in-memory LRU for active session.

## Data Flow (High level)

1. On startup or user-initiated refresh, enumerate games in the library.
2. For each game with a Steam AppId:
    - Check cache for ProtonDB and Steam Deck entries; serve if fresh.
    - Otherwise call `IProtonDbClient.GetGameSummary(appId)` and `ISteamDeckSource.GetCompatibility(appId)` in parallel.
    - Normalize responses via `MetadataProcessor`.
    - Convert normalized results to Playnite metadata actions via `MetadataMapper` according to settings.
    - Use `MetadataUpdater` to commit changes (or simulate in dry-run).

## API Contracts and Endpoints

- ProtonDB (community endpoints vary; plugin should call the stable summary endpoint if available):
   - Example request: `GET https://www.protondb.com/api/v1/reports/summaries/{appId}.json` (verify actual endpoint during implementation).
   - Expected fields: `tier`, `url` or `profile_url`, `counts` (optional). Plugin must tolerate missing fields.
   - Rate-limit guidance: conservative default of 1 request/second; configurable by user.

- Steam Deck compatibility sources (choose adapter):
   - Local Steam metadata (preferred for offline): parse local `appmanifest_<id>.acf` or Steam Deck metadata if available.
   - Steam Web API (if chosen): use Steam store or partner API where permitted. Note: Steam does not publicly expose Deck compatibility reliably — treat as experimental.

## Caching Strategy

- Cache entries keyed by `appId` with value: { source, normalizedResult, fetchedAt }.
- Default TTL: 24 hours (configurable).
- Storage: plugin folder under Playnite plugin data (`bin/Debug` during development). Use JSON files and directory locking to avoid corruption.
- Cache invalidation hooks: manual refresh, settings change, or TTL expiry.

## Rate Limiting & Retries

- Global per-plugin rate limiter with user-configurable limit (default: 1 req/sec) and burst allowance (N=3).
- Retries on transient network errors: up to 3 attempts with exponential backoff (200ms → 800ms → 1600ms).
- On persistent HTTP 429, back off respecting `Retry-After` header if present; escalate to circuit-breaker state after repeated 429s.

## Error Handling

- Failures are non-fatal: log and continue processing other games.
- Provide clear messages in Playnite logs for:
   - Network failures, JSON parse errors, unexpected payloads.
   - Cache read/write failures.
- Surface aggregate errors to the user via the settings UI with a 'Show last errors' button when repeated failures occur.

## Mapping Rules (Concrete)

- Steam Deck compatibility → Playnite categories/tags:
   - `Verified` → add categories: `Steam Deck Verified`, `Steam Deck`.
   - `Playable` → add categories: `Steam Deck Playable`, `Steam Deck`.
   - `Unsupported` → do not add (option: add `Steam Deck Unsupported` if user enables explicit tracking).
   - `Unknown` → no action.

- ProtonDB tier → Playnite categories/tags:
   - `Platinum` → `ProtonDB Platinum`, `ProtonDB`.
   - `Gold` → `ProtonDB Gold`, `ProtonDB`.
   - `Silver` → `ProtonDB Silver`, `ProtonDB`.
   - `Bronze` → `ProtonDB Bronze`, `ProtonDB`.
   - `Plausible`/`Borked`/`Unknown` → map to corresponding categories or only to `ProtonDB` based on settings.

- Links:
   - Add a link with title `ProtonDB` and URL returned by ProtonDB summary.

## Playnite Integration Details

- Use Playnite SDK APIs (from `Playnite.SDK` package) to enumerate and update games.
- Respect Playnite metadata update patterns: perform changes in the UI/main thread where required and use batched updates.
- Provide a background task that can be scheduled by the user for daily refreshes.

## Settings & Localization

- Expose toggles and fields in `SteamDeckProtonDbSettingsView.xaml`.
- Default settings: categories ON, tags OFF, links ON, cache TTL 24h, rate limit 1 req/sec.
- Add localization keys in `Localization/en_US.xaml` for all user-visible strings.

## Testing Plan

- Unit tests (where feasible) for:
   - JSON parsing for ProtonDB responses (mock payloads: full, partial, malformed).
   - Mapping rules (input enums → expected Playnite metadata operations).
   - Cache read/write and TTL expiry behavior.

- Integration tests:
   - A small integration harness that exercises `IProtonDbClient` against recorded HTTP responses (HTTP fixtures / VCR-style recordings).
   - End-to-end test with Playnite SDK mocked to verify `MetadataUpdater` performs expected calls.

## Security & Privacy

- Do not send Playnite user data beyond Steam AppId and public metadata.
- If storing API keys, persist encrypted or prefer instructing users to set keys in Playnite secure settings when possible.

## Build & Release Notes

- Target: .NET Framework 4.6.2, use Playnite SDK 6.x.
- Ensure `packages.config` references PlayniteSDK and include `Playnite.SDK.xml` for compilation.
- Provide `README.md` with installation steps, settings explanation, and troubleshooting (how to view plugin logs, clear cache, and enable dry-run).

## Implementation Roadmap (short)

1. Scaffold provider and settings UI.
2. Implement `IProtonDbClient` with parsing and caching.
3. Implement `ISteamDeckSource` adapter for local metadata and optional web API.
4. Implement mapping and updater integration with Playnite SDK.
5. Add caching, rate-limiting, retries, and tests.
6. QA, localization, docs, and publish.

---

If you'd like, I can now scaffold the provider skeleton and settings view (task 2) and wire up basic cache files for development.
