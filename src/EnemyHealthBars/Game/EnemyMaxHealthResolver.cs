using Auuueser.EnemyHealthBars.Core.GameData;

namespace Auuueser.EnemyHealthBars.Game;

internal sealed class EnemyMaxHealthResolver
{
    private readonly EnemyMaxHealthLookup maxHealthLookup = new(EnemyHealthDefaults.Items);

    public int Resolve(EnemyAI enemy, string displayName)
    {
        var cacheKey = ResolveCacheKey(enemy, displayName);
        var baseMaxHealth = ResolveBaseMaxHealth(enemy, cacheKey);
        var maxHealth = ResolveSpecialMaxHealth(enemy, baseMaxHealth);
        return maxHealth > 0 ? maxHealth + GetHealthOffset(enemy) : maxHealth;
    }

    // Manticoil and Tulip Snake HitEnemy kill on any hit without reading enemyHP.
    public static bool IsOneHitKill(EnemyAI enemy)
    {
        return enemy is DoublewingAI || enemy is FlowerSnakeEnemy;
    }

    // Effective health = enemyHP + offset, for enemies that only die below zero enemyHP.
    public static int GetHealthOffset(EnemyAI enemy)
    {
        return enemy is StingrayAI ? EnemySpecialMaxHealthRules.StingrayHealthOffset : 0;
    }

    public int UnresolvedCount => maxHealthLookup.UnresolvedCount;

    public string FormatUnresolvedNamesForDiagnostics(int maxNames)
    {
        return maxHealthLookup.FormatUnresolvedNamesForDiagnostics(maxNames);
    }

    private int ResolveBaseMaxHealth(EnemyAI enemy, string cacheKey)
    {
        if (maxHealthLookup.TryGetStrictMaxHealth(cacheKey, out var maxHealth))
        {
            return maxHealth;
        }

        if (maxHealthLookup.IsUnresolved(cacheKey))
        {
            return 0;
        }

        return maxHealthLookup.StoreResolvedStrictMaxHealth(cacheKey, ResolveFromPrefab(enemy));
    }

    private static int ResolveSpecialMaxHealth(EnemyAI enemy, int baseMaxHealth)
    {
        if (enemy is ButlerEnemyAI)
        {
            var connectedPlayersAmount = StartOfRound.Instance != null ? StartOfRound.Instance.connectedPlayersAmount : 1;
            return EnemySpecialMaxHealthRules.ResolveButlerMaxHealth(baseMaxHealth, connectedPlayersAmount);
        }

        if (enemy is CaveDwellerAI)
        {
            return EnemySpecialMaxHealthRules.ResolveManeaterMaxHealth(baseMaxHealth, enemy.currentBehaviourStateIndex);
        }

        return baseMaxHealth;
    }

    private static string ResolveCacheKey(EnemyAI enemy, string displayName)
    {
        var enemyType = enemy.enemyType;
        if (enemyType != null && !string.IsNullOrWhiteSpace(enemyType.enemyName))
        {
            return enemyType.enemyName;
        }

        if (!string.IsNullOrWhiteSpace(displayName))
        {
            return displayName;
        }

        return string.IsNullOrWhiteSpace(enemy.name) ? string.Empty : enemy.name;
    }

    private static int ResolveFromPrefab(EnemyAI enemy)
    {
        var enemyType = enemy.enemyType;
        if (enemyType == null || enemyType.enemyPrefab == null)
        {
            return 0;
        }

        if (!enemyType.enemyPrefab.TryGetComponent<EnemyAI>(out var prefabEnemy))
        {
            return 0;
        }

        return prefabEnemy.enemyHP > 0 ? prefabEnemy.enemyHP : 0;
    }
}
