namespace Auuueser.EnemyHealthBars.Core.Domain;

public readonly record struct EnemyHealthSnapshot(
    int EnemyId,
    string DisplayName,
    int CurrentHealth,
    int MaxHealth,
    bool IsDead,
    bool IsMaxHealthSettling = false,
    bool IsSpawnHealthSettling = false,
    bool IsClientHealthDesynced = false)
{
    public float HealthFraction
    {
        get
        {
            if (MaxHealth <= 0)
            {
                return 0f;
            }

            var fraction = (float)CurrentHealth / MaxHealth;
            if (fraction < 0f)
            {
                return 0f;
            }

            return fraction > 1f ? 1f : fraction;
        }
    }

    public bool IsFullHealth => !IsDead && MaxHealth > 0 && CurrentHealth >= MaxHealth;
}
