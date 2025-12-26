# Project Rules
- Use the dotnet command line to build and test the project.
- The main code exists in the SteamDeckProtonDb directory. Tests exist in the SteamDeckProtonDb.Tests directory.
- Always run tests after building by using `dontnet build && dotnet run`.
- If modifying the settings view XAML file (SteamDeckProtonDbSettingsView.xaml), ensure it is copied to the output directory by running `dotnet clean && dotnet build` and verifying the file exists in bin/Debug. Follow the rules in .ai-rules.md for XAML settings view handling.
- Prefer using tools to work with files.