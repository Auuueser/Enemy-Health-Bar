using System.Collections.Generic;
using Auuueser.EnemyHealthBars.Core.Configuration;

namespace Auuueser.EnemyHealthBars.Core.Domain;

public sealed class EnemyHealthTracker
{
    private readonly Dictionary<int, ObservedEnemyHealth> trackedEnemies = new();
    private readonly List<int> removedEnemyIds = new();

    public EnemyHealthSnapshot Track(EnemyHealthSample sample, MaxHealthMode maxHealthMode = MaxHealthMode.VanillaStrict)
    {
        if (!trackedEnemies.TryGetValue(sample.EnemyId, out var observedHealth))
        {
            observedHealth = ObservedEnemyHealth.FromSample(sample.CurrentHealth);
            trackedEnemies.Add(sample.EnemyId, observedHealth);
        }
        else
        {
            observedHealth.ApplySample(sample.CurrentHealth);
        }

        var currentHealth = observedHealth.CurrentHealth;
        var maxHealth = ResolveMaxHealth(sample, observedHealth, maxHealthMode);

        return new EnemyHealthSnapshot(
            sample.EnemyId,
            sample.DisplayName,
            currentHealth,
            maxHealth,
            sample.IsDead);
    }

    private static int ResolveMaxHealth(EnemyHealthSample sample, ObservedEnemyHealth observedHealth, MaxHealthMode maxHealthMode)
    {
        if (sample.MaxHealth < 0)
        {
            return 0;
        }

        if (maxHealthMode == MaxHealthMode.Adaptive || maxHealthMode == MaxHealthMode.Hybrid)
        {
            return observedHealth.MaxObservedHealth;
        }

        var maxHealth = sample.MaxHealth > 0 ? sample.MaxHealth : observedHealth.MaxObservedHealth;
        return maxHealth < observedHealth.CurrentHealth ? observedHealth.CurrentHealth : maxHealth;
    }

    public void Remove(int enemyId)
    {
        trackedEnemies.Remove(enemyId);
    }

    public void KeepOnly(ISet<int> activeEnemyIds)
    {
        removedEnemyIds.Clear();

        foreach (var enemyId in trackedEnemies.Keys)
        {
            if (!activeEnemyIds.Contains(enemyId))
            {
                removedEnemyIds.Add(enemyId);
            }
        }

        foreach (var enemyId in removedEnemyIds)
        {
            trackedEnemies.Remove(enemyId);
        }
    }
}
