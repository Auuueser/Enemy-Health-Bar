namespace Auuueser.EnemyHealthBars.Core.Domain;

public readonly record struct EnemyHealthSyncSnapshot(
    ulong NetworkObjectId,
    int CurrentHealth,
    int MaxHealth,
    bool IsDead,
    uint Sequence,
    bool IsMaxHealthSettling = false,
    bool IsSpawnHealthSettling = false,
    int EnemyId = 0)
{
    public EnemyHealthSnapshot ToEnemyHealthSnapshot(int enemyId, string displayName)
    {
        return new EnemyHealthSnapshot(
            enemyId,
            displayName,
            CurrentHealth,
            MaxHealth,
            IsDead,
            IsMaxHealthSettling,
            IsSpawnHealthSettling);
    }
}
