Title: Refactor ProtonDB client, parsing, caching, and provider behavior

Summary:
- Centralize and reuse HttpClient; add constructors to allow injection.
- Replace brittle regex JSON parsing with DataContractJsonSerializer-based extraction and a safe regex fallback for simple payloads.
- Add logging for HTTP, parsing, and cache operations.
- Make ProtonDB API URL and cache TTL configurable via `SteamDeckProtonDbSettings`.
- Add `UseFileCache` setting and default to `FileCacheManager` when enabled; create plugin cache directory.
- Refactor `MetadataFetcher` to async/await, add cache helpers, and avoid blocking `.GetResult()` calls in provider (use cached values + background refresh).
- Allow injecting HttpClient into `LocalSteamDeckSource` for testability.
- Add unit tests for `LocalSteamDeckSource` and `MetadataFetcher`; fix test project references.

Files changed (key):
- SteamDeckProtonDb/ProtonDbClient.cs
- SteamDeckProtonDb/SteamDeckSource.cs
- SteamDeckProtonDb/SteamDeckProtonDbProvider.cs
- SteamDeckProtonDb/SteamDeckProtonDbSettings.cs
- SteamDeckProtonDb.Tests/* (added tests and fixes)

Notes for reviewers:
- The provider now uses a file-backed cache by default; ensure plugin user data path permissions are acceptable.
- Tests run via NUnitLite for .NET Framework (as before). I validated the build and ran the tests locally; all tests passed.

Suggested next steps:
- Consider switching to Newtonsoft.Json for clearer JSON handling if more complex parsing is needed.
- Remove or use the `ownsHttpClient` flag to eliminate the current compiler warning.
- Push the branch and open a PR for team review.
