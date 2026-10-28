using System.Collections.Generic;
using Unity.Netcode;

namespace Auuueser.EnemyHealthBars.Game;

// RoundManager.SpawnedEnemies misses some live enemies: clients only register an enemy in
// EnemyAI.Start, and MaskedPlayerEnemy.Start never calls base.Start. Spawned network objects
// are rescanned for such unlisted enemies whenever their count changes, plus a slow fallback.
internal sealed class EnemySource
{
    private const float UnlistedEnemyRescanInterval = 2f;

    private readonly List<EnemyAI> combinedEnemies = new();
    private readonly List<EnemyAI> unlistedEnemies = new();
    private int lastSpawnedObjectCount = -1;
    private float nextUnlistedEnemyRescanTime;

    public int UnlistedEnemyCount => unlistedEnemies.Count;

    public int GetListedEnemyCount()
    {
        var roundManager = RoundManager.Instance;
        return roundManager != null && roundManager.SpawnedEnemies != null ? roundManager.SpawnedEnemies.Count : -1;
    }

    public List<EnemyAI>? GetEnemies(float currentTime)
    {
        var roundManager = RoundManager.Instance;
        var spawnedEnemies = roundManager != null ? roundManager.SpawnedEnemies : null;
        if (spawnedEnemies == null)
        {
            ResetUnlistedEnemies();
            return null;
        }

        RefreshUnlistedEnemies(spawnedEnemies, currentTime);
        if (unlistedEnemies.Count == 0)
        {
            return spawnedEnemies;
        }

        combinedEnemies.Clear();
        combinedEnemies.AddRange(spawnedEnemies);
        for (var i = 0; i < unlistedEnemies.Count; i++)
        {
            var enemy = unlistedEnemies[i];
            if (enemy != null && !spawnedEnemies.Contains(enemy))
            {
                combinedEnemies.Add(enemy);
            }
        }

        return combinedEnemies;
    }

    private void RefreshUnlistedEnemies(List<EnemyAI> spawnedEnemies, float currentTime)
    {
        var networkManager = NetworkManager.Singleton;
        var spawnManager = networkManager != null && networkManager.IsListening ? networkManager.SpawnManager : null;
        if (spawnManager == null)
        {
            ResetUnlistedEnemies();
            return;
        }

        var spawnedObjects = spawnManager.SpawnedObjectsList;
        if (spawnedObjects.Count == lastSpawnedObjectCount && currentTime < nextUnlistedEnemyRescanTime)
        {
            return;
        }

        lastSpawnedObjectCount = spawnedObjects.Count;
        nextUnlistedEnemyRescanTime = currentTime + UnlistedEnemyRescanInterval;
        unlistedEnemies.Clear();

        foreach (var networkObject in spawnedObjects)
        {
            if (networkObject != null &&
                networkObject.TryGetComponent<EnemyAI>(out var enemy) &&
                !spawnedEnemies.Contains(enemy))
            {
                unlistedEnemies.Add(enemy);
            }
        }
    }

    private void ResetUnlistedEnemies()
    {
        unlistedEnemies.Clear();
        combinedEnemies.Clear();
        lastSpawnedObjectCount = -1;
    }
}
