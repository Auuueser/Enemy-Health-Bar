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
    public bool TryRead(EnemyAI? enemy, bool includeInvulnerableEnemies, EnemyMaxHealthResolver maxHealthResolver, out EnemyHealthSample sample, out EnemyHealthReadFailure failure)
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

        if (!includeInvulnerableEnemies && !enemy.enemyType.canDie)
        {
            failure = EnemyHealthReadFailure.CannotDie;
            return false;
        }

        var enemyId = enemy.GetInstanceID();
        var displayName = ResolveDisplayName(enemy);
        var maxHealth = maxHealthResolver.Resolve(enemy, displayName);
        var networkObjectId = enemy.NetworkObject != null ? enemy.NetworkObject.NetworkObjectId : 0UL;
        var hasAuthoritativeMaxHealth = false;
        if (EnemyHealthOverrideStore.TryGetMaxHealth(enemyId, networkObjectId, out var authoritativeMaxHealth))
        {
            maxHealth = authoritativeMaxHealth;
            hasAuthoritativeMaxHealth = true;
        }

        sample = new EnemyHealthSample(
            enemyId,
            displayName,
            enemy.enemyHP,
            maxHealth,
            enemy.isEnemyDead,
            networkObjectId,
            hasAuthoritativeMaxHealth);

        return true;
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
