using Auuueser.EnemyHealthBars.Core.Domain;
using UnityEngine;

namespace Auuueser.EnemyHealthBars.Game;

internal enum EnemyHealthReadFailure
{
    None,
    MissingEnemy,
    MissingEnemyType,
    CannotDie,
}

internal sealed class EnemyHealthReader
{
    private readonly EnemyMaxHealthResolver maxHealthResolver;
    private readonly EnemyHitLedger hitLedger;

    public EnemyHealthReader(EnemyMaxHealthResolver maxHealthResolver, EnemyHitLedger hitLedger)
    {
        this.maxHealthResolver = maxHealthResolver;
        this.hitLedger = hitLedger;
    }

    public bool TryRead(EnemyAI? enemy, bool includeInvulnerableEnemies, out EnemyHealthSample sample, out EnemyHealthReadFailure failure)
    {
        sample = default!;
        failure = EnemyHealthReadFailure.None;

        if (enemy == null)
        {
            failure = EnemyHealthReadFailure.MissingEnemy;
            return false;
        }

        if (enemy.enemyType == null)
        {
            failure = EnemyHealthReadFailure.MissingEnemyType;
            return false;
        }

        if (!includeInvulnerableEnemies && !CanTakeHitDamage(enemy))
        {
            failure = EnemyHealthReadFailure.CannotDie;
            return false;
        }

        var enemyId = enemy.GetInstanceID();
        var displayName = ResolveDisplayName(enemy);
        if (EnemyMaxHealthResolver.IsOneHitKill(enemy))
        {
            sample = new EnemyHealthSample(
                enemyId,
                displayName,
                enemy.isEnemyDead ? 0 : 1,
                1,
                enemy.isEnemyDead,
                enemy.NetworkObject != null ? enemy.NetworkObject.NetworkObjectId : 0UL,
                HasAuthoritativeMaxHealth: true);
            return true;
        }

        var maxHealth = maxHealthResolver.Resolve(enemy, displayName);
        var healthOffset = EnemyMaxHealthResolver.GetHealthOffset(enemy);
        var networkObjectId = enemy.NetworkObject != null ? enemy.NetworkObject.NetworkObjectId : 0UL;
        var hasAuthoritativeMaxHealth = false;
        if (EnemyHealthOverrideStore.TryGetMaxHealth(enemyId, networkObjectId, out var authoritativeMaxHealth))
        {
            maxHealth = authoritativeMaxHealth + healthOffset;
            hasAuthoritativeMaxHealth = true;
        }

        sample = new EnemyHealthSample(
            enemyId,
            displayName,
            enemy.enemyHP + healthOffset,
            maxHealth,
            enemy.isEnemyDead,
            networkObjectId,
            hasAuthoritativeMaxHealth,
            ResolveHealthBeforeFirstHit(enemyId, healthOffset));

        return true;
    }

    private int ResolveHealthBeforeFirstHit(int enemyId, int healthOffset)
    {
        var healthBeforeFirstHit = hitLedger.GetHealthBeforeFirstHit(enemyId);
        return healthBeforeFirstHit > 0 ? healthBeforeFirstHit + healthOffset : 0;
    }

    private static bool CanTakeHitDamage(EnemyAI enemy)
    {
        // These vanilla enemies are marked canDie, but their HitEnemy never lowers enemyHP.
        return enemy.enemyType.canDie &&
            enemy is not BlobAI &&
            enemy is not DocileLocustBeesAI &&
            enemy is not TestEnemy;
    }

    private static string ResolveDisplayName(EnemyAI enemy)
    {
        var enemyType = enemy.enemyType;
        if (enemyType != null && !string.IsNullOrWhiteSpace(enemyType.enemyName))
        {
            return enemyType.enemyName;
        }

        return string.IsNullOrWhiteSpace(enemy.name) ? "Enemy" : enemy.name;
    }
}
