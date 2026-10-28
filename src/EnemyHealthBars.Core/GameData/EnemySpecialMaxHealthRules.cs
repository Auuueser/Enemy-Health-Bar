namespace Auuueser.EnemyHealthBars.Core.GameData;

public static class EnemySpecialMaxHealthRules
{
    // ButlerEnemyAI.Start lowers prefab HP from 8 to 2 when no clients are connected.
    private const int ButlerSinglePlayerMaxHealth = 2;
    // CaveDwellerAI baby state reacts to hits by growing/scaring, but does not lose enemyHP.
    private const int ManeaterBabyKillableMaxHealth = -1;

    // StingrayAI.HitEnemy only kills when enemyHP drops below zero, so it survives one more hit.
    public const int StingrayHealthOffset = 1;

    public static int ResolveButlerMaxHealth(int generatedMaxHealth, int connectedPlayersAmount)
    {
        return connectedPlayersAmount == 0 ? ButlerSinglePlayerMaxHealth : generatedMaxHealth;
    }

    public static int ResolveManeaterMaxHealth(int generatedMaxHealth, int currentBehaviourStateIndex)
    {
        return currentBehaviourStateIndex == 0 ? ManeaterBabyKillableMaxHealth : generatedMaxHealth;
    }
}
