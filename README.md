# Enemy Health Bar

Enemy Health Bar is a client-side Lethal Company plugin that displays enemy health in world-space UI. It does not send network messages and does not modify enemy state.

## Features

- Displays enemy health bars above enemies, as compact side bars, or as numbers only.
- Shows current and maximum health values with configurable text formats.
- Supports adaptive maximum health handling for modded enemies that increase or reduce health.
- Preserves vanilla special cases for enemies whose effective health differs by state or player count.
- Keeps debug diagnostics opt-in through configuration.
- Supports live configuration reloads while the game is running.
- Provides optional LethalConfig integration when LethalConfig is installed.
- Uses localized configuration text when LC Chinese Project is detected.

## Installation

Install through a Thunderstore-compatible mod manager, or manually extract the package contents into the game folder so that the DLLs are placed under:

```text
BepInEx/plugins/EnemyHealthBar/
```

The package requires BepInEx for Lethal Company. LethalConfig and LC Chinese Project are optional integrations, not required dependencies.

## Building from source

Set the following environment variables before building:

- `LETHAL_COMPANY_ROOT`: path to the local Lethal Company install.
- `R2MODMAN_PROFILE_ROOT`: path to a Lethal Company r2modman profile containing BepInEx and, optionally, LethalConfig.

Then build the plugin project:

```powershell
dotnet build src/EnemyHealthBars/EnemyHealthBars.csproj -c Release
```

## Configuration

The plugin creates a BepInEx configuration file on first launch. Most settings can be changed while the game is running.

Important options:

- `DisplayMode`: `HorizontalBar`, `VerticalSideBar`, or `NumbersOnly`.
- `ShowHealthNumbers`: shows current and maximum health text.
- `HealthTextFormat`: controls current/max, current-only, or percent-only text.
- `MaxHealthMode`: `Hybrid` by default. This keeps vanilla special-case handling while adapting to observed modded health values.
- `MaxDistance`: hides health bars beyond the configured range.
- `Debug.Enabled`: unlocks diagnostics and test-only visibility options.

## Notes

- This is a client-side display mod. It does not alter combat, enemy AI, spawning, or networking.
- Full-health enemies are hidden by default during normal play.
- Some vanilla enemies have phase-specific or player-count-specific rules. The plugin handles these cases without runtime reflection or scene-wide searches.

## License

Enemy Health Bar is licensed under GPL-3.0.
