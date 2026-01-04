# Steam Deck ProtonDB Plugin for Playnite

A Playnite plugin that automatically adds Steam Deck compatibility and ProtonDB ratings to your game library. Organize your games with categories and tags to see what works great on Steam Deck and Linux.

## Features

- **Steam Deck Compatibility**: Shows which games are Verified, Playable, or Unsupported on Steam Deck
- **ProtonDB Ratings**: Adds community-driven Linux compatibility ratings (Platinum, Gold, Silver, Bronze, Borked)
- **Automatic Organization**: Creates categories and tags for easy filtering
- **Direct Links**: Adds links to ProtonDB pages for more details
- **Smart Caching**: Remembers fetched data for 24 hours to avoid repeated lookups

## Installation

1. Download the latest `.pext` file from the [Releases](https://github.com/mobilla/Playnite-SteamDeckProtonDB/releases) page
2. Double-click the file to install it in Playnite (or drag it to Playnite)
3. Restart Playnite if prompted
4. The plugin will appear in your list of metadata providers

## Usage

### Fetch Compatibility Data

1. Right-click any game in your library
2. Select **Edit** → **Download Metadata**
3. Check the **Steam Deck ProtonDB** provider
4. Click **Download**
5. Review and accept the suggested metadata

The plugin will add:
- Categories like "Steam Deck - Verified" or "ProtonDB - Platinum"
- Tags like `steamdeck:verified` or `protondb:gold`
- A link to the game's ProtonDB page

### Filter Your Library

After fetching metadata, you can filter your games:
- **View Steam Deck Verified games**: Filter by "Steam Deck - Verified" category
- **View ProtonDB Gold games**: Filter by "ProtonDB - Gold" category
- **Use tags for quick searches**: Search for `steamdeck:verified` or `protondb:platinum`

## Configuration

Access settings from: **Playnite Menu** → **Add-ons** → **Extension Settings** → **Steam Deck ProtonDB**

### Available Options

- **Enable Steam Deck Categories**: Add Steam Deck compatibility categories and tags
- **Enable ProtonDB Categories**: Add ProtonDB tier categories and tags
- **Enable ProtonDB Link**: Add a link to the game's ProtonDB page
- **Enable Tags**: Add searchable tags (requires categories to be enabled)
- **Cache Duration**: How long to remember fetched data (default: 24 hours)

## Troubleshooting

### Plugin doesn't appear in metadata providers
- Make sure you have Playnite 6.2.0 or later
- Try restarting Playnite
- Check that the plugin is enabled in Add-ons settings

### No data is being fetched
- Ensure the game has a Steam App ID (the plugin only works with Steam games)
- Check your internet connection
- View Playnite's log file for error details (Menu → About Playnite → Open → Log file)

### Data seems outdated
- The plugin caches data for 24 hours to avoid excessive API calls
- To refresh, increase the cache duration to force a new fetch, or clear the cache by reinstalling the plugin

### Slow metadata downloads
- The plugin respects API rate limits to be a good citizen
- Fetching many games at once will take longer due to rate limiting
- First fetch takes 1-2 seconds per game; cached data returns instantly

## What Data Comes From Where

- **Steam Deck Compatibility**: Official data from Steam's Deck compatibility API
- **ProtonDB Ratings**: Community reports from [ProtonDB.com](https://www.protondb.com)

Note: ProtonDB data depends on community reports and may not reflect the very latest game updates.

## Support

- **Report bugs or request features**: [GitHub Issues](https://github.com/mobilla/Playnite-SteamDeckProtonDB/issues)
- **Contribute**: See [CONTRIBUTORS.md](CONTRIBUTORS.md)
- **Developer documentation**: See [docs/DEVELOPERS.md](docs/DEVELOPERS.md)

## License

This plugin is open source under the MIT License. See [LICENSE](LICENSE) for details.

## Credits

- **Playnite**: For the excellent game library manager and extension framework
- **Valve**: For Steam Deck and public API access
- **ProtonDB Community**: For compatibility data and testing reports

---

**Version**: 1.0.0  
**License**: MIT
