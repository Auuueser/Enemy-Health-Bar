using System.Collections.Generic;
using Auuueser.EnemyHealthBars.Core.Configuration;

namespace Auuueser.EnemyHealthBars.Core.Domain;

public sealed class EnemyHealthTracker
{
    private readonly Dictionary<int, ObservedEnemyHealth> trackedEnemies = new();
    private readonly List<int> removedEnemyIds = new();

    public EnemyHealthSnapshot Track(EnemyHealthSample sample, MaxHealthMode maxHealthMode = MaxHealthMode.VanillaStrict)
    {
        var trackedCurrentHealth = ResolveTrackedCurrentHealth(sample);
        if (!trackedEnemies.TryGetValue(sample.EnemyId, out var observedHealth))
        {
            observedHealth = ObservedEnemyHealth.FromSample(
                trackedCurrentHealth,
                !sample.HasAuthoritativeMaxHealth && ShouldSettleInitialFullHealth(sample, maxHealthMode),
                ResolveInitialMaxObservedHealthFloor(sample));
            if (sample.HasAuthoritativeMaxHealth)
            {
                observedHealth.ApplyAuthoritativeSample(trackedCurrentHealth, sample.MaxHealth);
            }

            trackedEnemies.Add(sample.EnemyId, observedHealth);
        }
        else if (sample.HasAuthoritativeMaxHealth)
        {
            observedHealth.ApplyAuthoritativeSample(trackedCurrentHealth, sample.MaxHealth);
        }
        else
        {
            observedHealth.ApplySample(trackedCurrentHealth, AllowsAdaptiveMaxHealth(maxHealthMode), sample.MaxHealth);
        }

        var currentHealth = observedHealth.CurrentHealth;
        var maxHealth = ResolveMaxHealth(sample, observedHealth, maxHealthMode);
        var isClientHealthDesynced = IsAliveZeroHealthSample(sample);

        return new EnemyHealthSnapshot(
            sample.EnemyId,
            sample.DisplayName,
            currentHealth,
            maxHealth,
            sample.IsDead,
            observedHealth.IsMaxHealthSettling,
            observedHealth.IsSpawnHealthSettling,
            isClientHealthDesynced);
    }

    private static int ResolveMaxHealth(EnemyHealthSample sample, ObservedEnemyHealth observedHealth, MaxHealthMode maxHealthMode)
    {
        if (sample.MaxHealth < 0)
        {
            return 0;
        }

        if (sample.HasAuthoritativeMaxHealth)
        {
            return sample.MaxHealth < observedHealth.CurrentHealth ? observedHealth.CurrentHealth : sample.MaxHealth;
        }

        if (AllowsAdaptiveMaxHealth(maxHealthMode))
        {
            return observedHealth.MaxObservedHealth;
        }

        var maxHealth = sample.MaxHealth > 0 ? sample.MaxHealth : observedHealth.MaxObservedHealth;
        return maxHealth < observedHealth.CurrentHealth ? observedHealth.CurrentHealth : maxHealth;
    }

    private static bool AllowsAdaptiveMaxHealth(MaxHealthMode maxHealthMode)
    {
        return maxHealthMode == MaxHealthMode.Adaptive || maxHealthMode == MaxHealthMode.Hybrid;
    }

    private static bool ShouldSettleInitialFullHealth(EnemyHealthSample sample, MaxHealthMode maxHealthMode)
    {
        return AllowsAdaptiveMaxHealth(maxHealthMode) &&
            !sample.IsDead &&
            sample.CurrentHealth > 0 &&
            sample.MaxHealth > 0 &&
            sample.CurrentHealth == sample.MaxHealth;
    }

    private static int ResolveTrackedCurrentHealth(EnemyHealthSample sample)
    {
        return IsAliveZeroHealthSample(sample) ? 1 : sample.CurrentHealth;
    }

    private static bool IsAliveZeroHealthSample(EnemyHealthSample sample)
    {
        return !sample.IsDead && sample.CurrentHealth <= 0;
    }

    private static int ResolveInitialMaxObservedHealthFloor(EnemyHealthSample sample)
    {
        return IsAliveZeroHealthSample(sample) && sample.MaxHealth > 0 ? sample.MaxHealth : 0;
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
