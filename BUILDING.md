# Building Enemy Health Bar

Requires .NET SDK 8 or newer and a local Lethal Company V81 installation.

Set these environment variables before building:

- `LETHAL_COMPANY_ROOT`: the local Lethal Company install.
- `R2MODMAN_PROFILE_ROOT`: a Lethal Company r2modman profile containing BepInEx and, optionally, LethalConfig.

```powershell
dotnet build src/EnemyHealthBars/EnemyHealthBars.csproj -c Release
```

Install both output assemblies from `src/EnemyHealthBars/bin/Release/netstandard2.1/` together in `BepInEx/plugins/EnemyHealthBar/`:

- `EnemyHealthBar.dll`
- `EnemyHealthBars.Core.dll`

Do not package game assemblies, BepInEx, Harmony or LethalConfig; they are referenced from the local install only.
