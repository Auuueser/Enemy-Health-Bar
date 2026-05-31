using System.Collections.Generic;

namespace Auuueser.EnemyHealthBars.Game;

internal sealed class RoundManagerEnemySource
{
    public List<EnemyAI>? GetSpawnedEnemies()
    {
        var roundManager = RoundManager.Instance;
        return roundManager != null ? roundManager.SpawnedEnemies : null;
    }
}
