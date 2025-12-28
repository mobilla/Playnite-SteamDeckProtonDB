# Project Rules
- Use the restore-and-build.ps1 script in the SteamDeckProtonDb directory to build and test the project. Run it with: `./SteamDeckProtonDb/restore-and-build.ps1`
- The main code exists in the SteamDeckProtonDb directory. Tests exist in the SteamDeckProtonDb.Tests directory.
- The restore-and-build.ps1 script automatically restores NuGet packages, builds the main project, builds the test project, and runs all tests.
- If modifying the settings view XAML file (SteamDeckProtonDbSettingsView.xaml), ensure it is copied to the output directory by running the restore-and-build.ps1 script and verifying the file exists in bin/Debug. Follow the rules in .ai-rules.md for XAML settings view handling.
- Prefer using tools to work with files.