namespace Auuueser.EnemyHealthBars.Core.Domain;

public sealed class ObservedEnemyHealth
{
    private ObservedEnemyHealth(int currentHealth, int maxObservedHealth)
    {
        CurrentHealth = currentHealth;
        MaxObservedHealth = maxObservedHealth;
    }

    public int CurrentHealth { get; private set; }

    public int MaxObservedHealth { get; private set; }

    public float HealthFraction
    {
        get
        {
            if (MaxObservedHealth <= 0)
            {
                return 0f;
            }

            var fraction = (float)CurrentHealth / MaxObservedHealth;
            if (fraction < 0f)
            {
                return 0f;
            }

            return fraction > 1f ? 1f : fraction;
        }
    }

    public static ObservedEnemyHealth FromSample(int currentHealth)
    {
        var normalizedHealth = NormalizeHealth(currentHealth);
        return new ObservedEnemyHealth(normalizedHealth, normalizedHealth);
    }

    public void ApplySample(int currentHealth)
    {
        CurrentHealth = NormalizeHealth(currentHealth);

        if (CurrentHealth > MaxObservedHealth)
        {
            MaxObservedHealth = CurrentHealth;
        }
    }

    private static int NormalizeHealth(int health)
    {
        return health < 0 ? 0 : health;
    }
}

