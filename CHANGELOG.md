# Changelog

## 0.0.2

- Added optional host-authority health sync for lobbies where host-side mods change enemy health values.
- Added client-side prediction rebasing from host snapshots to keep synced health display and local hit behavior aligned in host-modded lobbies.
- Added spawn-settle suppression to avoid briefly showing temporary vanilla health before host-side health changes apply.
- Added client fallback handling for alive enemies whose local predicted health reaches zero before host death confirmation.
- Improved adaptive maximum health handling for enemies with increased or reduced health values.
- Improved vanilla special-case handling for Butler, Maneater, and Masked enemies.

## 0.0.1

- Added client-side enemy health bars for Lethal Company.
- Added horizontal bar, side vertical bar, and numbers-only display modes.
- Added current/max, current-only, and percent-only health text formats.
- Added adaptive maximum health handling for observed enemy health values.
- Added live BepInEx configuration reloads.
- Added optional LethalConfig integration.
- Added optional Chinese configuration text when LC Chinese Project is detected.
- Added opt-in debug diagnostics and a test health bar.
