# Progress Bar Implementation Notes

## Current Implementation Status

### 1. Metadata Provider (OnDemandMetadataProvider)
**Location:** `SteamDeckProtonDbProvider.cs`

**How Playnite handles progress:**
- When you use "Download Metadata" from the game editor or right-click menu, Playnite shows its OWN progress dialog
- The `Get*` methods (GetTags, GetFeatures, GetLinks) are called synchronously
- Playnite manages the progress UI automatically
- Each method should return quickly (within seconds)

**Current implementation:**
- ✅ Methods fetch data synchronously with `.Wait()` (15 second timeout)
- ✅ Uses cache when available (returns immediately)
- ✅ Fresh data is fetched from APIs when not cached
- ⚠️ Playnite's progress dialog SHOULD show automatically, but may not update per-field

**Important:** Metadata provider methods CANNOT show their own progress dialogs - Playnite controls this.

### 2. Menu Item (Manual Bulk Update)
**Location:** `SteamDeckProtonDb.cs` → `AddTagsAndLinksToGames` method

**Implementation:**
- ✅ Shows custom progress dialog with `ActivateGlobalProgress`
- ✅ Displays game name and progress (X/Y games)
- ✅ Has cancel button (`Cancelable = true`)
- ✅ Updates progress bar as each game is processed
- ✅ Uses `BufferedUpdate()` to avoid flooding other plugins with events

This method DOES show progress and should work correctly.

## Expected Behavior

### When using "Download Metadata" feature:
1. Select game(s) → Right-click → Edit → Download Metadata
2. Choose metadata providers including "Steam Deck & ProtonDB"
3. Select Tags, Features, Links options
4. Click Download
5. **Playnite shows a progress dialog** (managed by Playnite, not the plugin)
6. Each field is downloaded sequentially
7. Dialog closes when complete

### When using the menu item:
1. Select game(s)
2. Extensions menu → @Steam Deck ProtonDB → "Add Steam Deck/ProtonDB tags and link"
3. **Plugin shows its own progress dialog** with:
   - Title: "Adding Steam Deck/ProtonDB tags and link"
   - Progress bar showing X/Y games
   - Cancel button
   - Status text updating per game
4. Dialog closes when complete or cancelled

## Troubleshooting

If you're not seeing progress when using "Download Metadata":
- This is controlled by Playnite, not the plugin
- The plugin methods are being called correctly
- Progress dialog should appear automatically from Playnite
- If it doesn't, this might be a Playnite UI issue or the download is happening too fast (cached data)

If you're not seeing progress when using the menu item:
- This should definitely show - check that Playnite isn't minimized
- Check the debug log for any errors
- Make sure games are selected before clicking the menu item

## Changes Made

### Latest changes (Dec 23, 2025):
1. ✅ Removed incorrect nested progress dialogs from metadata provider
2. ✅ Kept simple synchronous fetching (as Playnite expects)
3. ✅ Added `BufferedUpdate()` to menu command to batch database changes
4. ✅ Menu command already had full progress implementation

The metadata provider now works as Playnite expects - it returns data synchronously and lets Playnite handle the UI.
