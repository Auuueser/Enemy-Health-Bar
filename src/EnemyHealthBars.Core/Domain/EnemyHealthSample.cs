namespace Auuueser.EnemyHealthBars.Core.Domain;

public readonly record struct EnemyHealthSample(
    int EnemyId,
    string DisplayName,
    int CurrentHealth,
    int MaxHealth,
    bool IsDead,
    ulong NetworkObjectId = 0UL,
    bool HasAuthoritativeMaxHealth = false)
{
    public EnemyHealthSample(
        int EnemyId,
        string DisplayName,
        int CurrentHealth,
        bool IsDead)
        : this(EnemyId, DisplayName, CurrentHealth, 0, IsDead)
    {
    }
}
