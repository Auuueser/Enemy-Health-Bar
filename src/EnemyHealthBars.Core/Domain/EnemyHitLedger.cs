using System.Collections.Generic;

namespace Auuueser.EnemyHealthBars.Core.Domain;

/// <summary>
/// Remembers each enemy's health immediately before its first hit, so early damage
/// is never mistaken for a mod lowering the enemy's spawn health.
/// </summary>
public sealed class EnemyHitLedger
{
    private readonly Dictionary<int, int> healthBeforeFirstHitByEnemyId = new();
    private readonly List<int> removedEnemyIds = new();

    public int Count => healthBeforeFirstHitByEnemyId.Count;

    public void RecordHit(int enemyId, int healthBeforeHit)
    {
        if (enemyId == 0 || healthBeforeHit <= 0 || healthBeforeFirstHitByEnemyId.ContainsKey(enemyId))
        {
            return;
        }

        healthBeforeFirstHitByEnemyId.Add(enemyId, healthBeforeHit);
    }

    public int GetHealthBeforeFirstHit(int enemyId)
    {
        return healthBeforeFirstHitByEnemyId.TryGetValue(enemyId, out var health) ? health : 0;
    }

    public void Clear()
    {
        healthBeforeFirstHitByEnemyId.Clear();
        removedEnemyIds.Clear();
    }

    public void KeepOnly(ISet<int> activeEnemyIds)
    {
        if (healthBeforeFirstHitByEnemyId.Count == 0)
        {
            return;
        }

        removedEnemyIds.Clear();
        foreach (var enemyId in healthBeforeFirstHitByEnemyId.Keys)
        {
            if (!activeEnemyIds.Contains(enemyId))
            {
                removedEnemyIds.Add(enemyId);
            }
        }

        foreach (var enemyId in removedEnemyIds)
        {
            healthBeforeFirstHitByEnemyId.Remove(enemyId);
        }
    }
}
