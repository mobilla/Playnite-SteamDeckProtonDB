# Progress Bar Implementation

## TL;DR
- **Metadata provider** (`SteamDeckProtonDbProvider.cs`): No custom progress - Playnite controls the UI
- **Menu item** (`AddTagsAndLinksToGames`): Shows custom progress dialog with cancel button
- Metadata methods fetch synchronously with 15s timeout, use cache when available

## Implementation

### Metadata Provider (OnDemandMetadataProvider)
- Methods: `GetTags()`, `GetFeatures()`, `GetLinks()`
- Returns data synchronously (uses `.Wait()` with 15s timeout)
- Playnite shows its own progress dialog automatically
- Cannot show custom progress dialogs

### Menu Item (Bulk Update)
- Command: Extensions → Steam Deck ProtonDB → "Add Steam Deck/ProtonDB tags and link"
- Shows progress via `ActivateGlobalProgress()`:
  - Title, progress bar (X/Y games), cancel button
  - Updates per game
  - Uses `BufferedUpdate()` to batch database changes

## Troubleshooting

| Issue | Cause | Solution |
|-------|-------|----------|
| No progress in "Download Metadata" | Playnite-controlled UI | Expected - Playnite manages this |
| No progress in menu item | Selection/UI issue | Verify games selected, check logs |
| Fast completion | Cached data | Working correctly |
