using System.Collections.Generic;

namespace Auuueser.EnemyHealthBars.Core.Domain;

public static class EnemyHealthOverrideStore
{
    private static readonly Dictionary<int, int> maxHealthByEnemyId = new();
    private static readonly Dictionary<ulong, int> maxHealthByNetworkObjectId = new();
    private static readonly List<int> removedEnemyIds = new();
    private static readonly List<ulong> removedNetworkObjectIds = new();

    public static void SetMaxHealth(int enemyId, int maxHealth)
    {
        SetMaxHealth(enemyId, 0UL, maxHealth);
    }

    public static void SetMaxHealth(int enemyId, ulong networkObjectId, int maxHealth)
    {
        if ((enemyId == 0 && networkObjectId == 0UL) || maxHealth <= 0)
        {
            Remove(enemyId, networkObjectId);
            return;
        }

        if (enemyId != 0)
        {
            maxHealthByEnemyId[enemyId] = maxHealth;
        }

        if (networkObjectId != 0UL)
        {
            maxHealthByNetworkObjectId[networkObjectId] = maxHealth;
        }
    }

    public static bool TryGetMaxHealth(int enemyId, out int maxHealth)
    {
        return TryGetMaxHealth(enemyId, 0UL, out maxHealth);
    }

    public static bool TryGetMaxHealth(int enemyId, ulong networkObjectId, out int maxHealth)
    {
        if (networkObjectId != 0UL && maxHealthByNetworkObjectId.TryGetValue(networkObjectId, out maxHealth))
        {
            return true;
        }

        return maxHealthByEnemyId.TryGetValue(enemyId, out maxHealth);
    }

    public static void Remove(int enemyId)
    {
        maxHealthByEnemyId.Remove(enemyId);
    }

    public static void Remove(int enemyId, ulong networkObjectId)
    {
        if (enemyId != 0)
        {
            maxHealthByEnemyId.Remove(enemyId);
        }

        if (networkObjectId != 0UL)
        {
            maxHealthByNetworkObjectId.Remove(networkObjectId);
        }
    }

    public static void Clear()
    {
        maxHealthByEnemyId.Clear();
        maxHealthByNetworkObjectId.Clear();
        removedEnemyIds.Clear();
        removedNetworkObjectIds.Clear();
    }

    public static void KeepOnly(ISet<int> activeEnemyIds)
    {
        if (maxHealthByEnemyId.Count == 0)
        {
            return;
        }

        removedEnemyIds.Clear();
        foreach (var enemyId in maxHealthByEnemyId.Keys)
        {
            if (!activeEnemyIds.Contains(enemyId))
            {
                removedEnemyIds.Add(enemyId);
            }
        }

        foreach (var enemyId in removedEnemyIds)
        {
            maxHealthByEnemyId.Remove(enemyId);
        }
    }

    public static void KeepOnly(ISet<int> activeEnemyIds, ISet<ulong> activeNetworkObjectIds)
    {
        KeepOnly(activeEnemyIds);

        if (maxHealthByNetworkObjectId.Count == 0)
        {
            return;
        }

        removedNetworkObjectIds.Clear();
        foreach (var networkObjectId in maxHealthByNetworkObjectId.Keys)
        {
            if (!activeNetworkObjectIds.Contains(networkObjectId))
            {
                removedNetworkObjectIds.Add(networkObjectId);
            }
        }

        foreach (var networkObjectId in removedNetworkObjectIds)
        {
            maxHealthByNetworkObjectId.Remove(networkObjectId);
        }
    }
}
