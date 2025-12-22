# SteamDeck ProtonDB Plugin for Playnite

A Playnite plugin that fetches Steam Deck compatibility and ProtonDB tier information for your game library, automatically organizing games into categories and tags based on their compatibility status.

## Features

- **Steam Deck Compatibility**: Automatically detects and categorizes games based on Steam Deck verification status (Verified, Playable, Unsupported)
- **ProtonDB Integration**: Fetches ProtonDB tier information (Platinum, Gold, Silver, Bronze, Plausible, Borked) for Linux gaming compatibility
- **Smart Categorization**: Organizes games into categories like "Steam Deck - Verified", "ProtonDB - Platinum", etc.
- **Tag System**: Adds tags for quick filtering (e.g., `steamdeck:verified`, `protondb:platinum`)
- **ProtonDB Links**: Adds direct links to ProtonDB game pages in game details
- **Configurable Behavior**: Toggle which metadata to fetch and apply via settings
- **Intelligent Caching**: Caches API responses with configurable TTL to minimize network calls

## Installation

1. Download the latest release from the [Releases](https://github.com/yourusername/playnite_protondb/releases) page
2. Extract the contents to your Playnite extensions folder:
   - Windows: `%APPDATA%\Playnite\Extensions\SteamDeckProtonDb`
   - Linux: `~/.config/Playnite/Extensions/SteamDeckProtonDb`
3. Restart Playnite
4. The plugin will appear in your list of metadata providers

## Configuration

Access plugin settings from Playnite's settings menu:

### Steam Deck Metadata
- **Enable Steam Deck Compatibility Categories**: Adds Steam Deck compatibility status as categories
  - Adds: "Steam Deck", "Steam Deck - Verified/Playable/Unsupported"
  - Tags: `steamdeck:verified`, `steamdeck:playable`, `steamdeck:unsupported`

### ProtonDB Metadata
- **Enable ProtonDB Tier Categories**: Adds ProtonDB compatibility tier as categories
  - Adds: "ProtonDB", "ProtonDB - Platinum/Gold/Silver/Bronze/Plausible/Borked"
  - Tags: `protondb:platinum`, `protondb:gold`, etc.
- **Enable ProtonDB Link**: Adds a clickable link to the game's ProtonDB page

### Additional Options
- **Enable Tags**: Toggles whether compatibility tags are added (requires categories enabled)
- **Cache TTL (minutes)**: How long to cache metadata before fetching fresh data (default: 1440 = 24 hours)
- **ProtonDB API URL**: The API endpoint for fetching ProtonDB data (default: official ProtonDB API)

## Usage

### On-Demand Metadata Fetch
The plugin works as an on-demand metadata provider in Playnite:

1. Right-click a game in your library
2. Select "Edit" → "Metadata" → "Download metadata"
3. Check the "SteamDeck ProtonDB" provider
4. Playnite will fetch Steam Deck and ProtonDB data and suggest metadata updates

### Automatic Categorization
The plugin automatically:
- Fetches data via the Steam Store API and ProtonDB API
- Maps compatibility information to Playnite categories
- Adds tags for quick filtering and organization
- Caches results to avoid repeated API calls

### Filtering Games
Use the added categories and tags to organize your library:
- View all Steam Deck Verified games: Filter by "Steam Deck - Verified" category
- View all Gold-tier ProtonDB games: Filter by "ProtonDB - Gold" category
- Use tags for more granular filtering

## How It Works

### Data Sources

1. **Steam Deck Compatibility**: Fetched from the Steam Store API (`/api/appdetails`) by parsing the Deck compatibility field
2. **ProtonDB Data**: Fetched from ProtonDB's official API (`https://www.protondb.com/api/v1/reports/summaries/{appid}.json`)

### Caching Strategy

The plugin implements a two-tier caching approach:
- **In-Memory Cache**: Fast access for frequently requested games (default)
- **File-Based Cache** (optional): Persistent cache across Playnite restarts (can be enabled in future versions)

Cache entries are automatically invalidated after the configured TTL (default: 24 hours).

### Error Handling

The plugin gracefully handles errors:
- Network failures fall back to cached data or return `Unknown` status
- Invalid AppIds are skipped
- Malformed API responses use fallback parsing strategies

## Requirements

- Playnite 6.2.0 or later
- .NET Framework 4.6.2 or later (Windows)
- Active internet connection for API calls

## Building from Source

### Prerequisites
- Visual Studio 2019 or later, or Visual Studio Code with C# extension
- .NET Framework 4.6.2 SDK
- PowerShell 5.1 or later

### Build Steps

```bash
cd SteamDeckProtonDb
dotnet build

# Run unit tests
cd ../SteamDeckProtonDb.Tests
dotnet run
```

## Testing

The plugin includes comprehensive unit tests for:
- ProtonDB JSON parsing (valid and malformed responses)
- Cache manager functionality (set, get, expiration, clear)
- Steam Deck API response parsing

Run tests with:
```bash
cd SteamDeckProtonDb.Tests
dotnet run
```

## Troubleshooting

### Plugin not appearing in metadata providers
- Ensure Playnite version 6.2.0 or later is installed
- Check that the plugin file is in the correct extensions folder
- Try restarting Playnite

### Metadata not being fetched
- Verify you have an active internet connection
- Check that the Steam AppId is correct for the game
- Review the Playnite log file for detailed error messages

### High CPU usage or slow metadata fetch
- The plugin uses configurable retry/backoff logic
- Increase the cache TTL to reduce repeated API calls
- Ensure your internet connection is stable

### Inconsistent data between Steam and ProtonDB
- Steam Deck compatibility is based on official Valve certification
- ProtonDB data is community-reported and may vary
- Some games may have recent changes not yet reflected in ProtonDB

## API Endpoints

The plugin calls the following public APIs:

1. **Steam Store API** (no API key required)
   - Endpoint: `https://store.steampowered.com/api/appdetails`
   - Rate limit: Generous for API calls
   - Fallback: Returns `Unknown` if unreachable

2. **ProtonDB API** (no API key required)
   - Endpoint: `https://www.protondb.com/api/v1/reports/summaries/{appid}.json`
   - Rate limit: Generous for API calls
   - Fallback: Returns `Unknown` if unreachable

Both APIs are public and free to use.

## Known Limitations

- The plugin requires a Steam AppId to fetch data (not all games have valid Steam AppIds)
- Steam Deck compatibility data may lag behind official Steam updates
- ProtonDB data depends on community reports and may not reflect the latest versions

## Contributing

Contributions are welcome! Please submit issues and pull requests to the [GitHub repository](https://github.com/yourusername/playnite_protondb).

## License

This plugin is provided as-is for use with Playnite. Please refer to the LICENSE file for details.

## Acknowledgments

- **Playnite**: For the excellent extension framework and SDK
- **Valve**: For Steam Deck and the public API endpoints
- **ProtonDB**: For community-driven compatibility data

## Support

For issues, questions, or feature requests, please open an issue on GitHub or contact the developer.

---

**Version**: 1.0.0  
**Last Updated**: December 2025  
**Author**: Steam Deck ProtonDB Plugin Contributors
