namespace Auuueser.EnemyHealthBars.Core.Domain;

public sealed class ObservedEnemyHealth
{
    private const int InitialFullHealthSettleSampleLimit = 8;

    private const int LowerMaxConfirmationSamples = 2;

    private ObservedEnemyHealth(int currentHealth, int maxObservedHealth, bool isSpawnHealthSettling)
    {
        CurrentHealth = currentHealth;
        MaxObservedHealth = maxObservedHealth;
        IsSpawnHealthSettling = isSpawnHealthSettling;
        SamplesSeen = 1;
    }

    public int CurrentHealth { get; private set; }

    public int MaxObservedHealth { get; private set; }

    public int SamplesSeen { get; private set; }

    public bool IsMaxHealthSettling { get; private set; }

    public bool IsSpawnHealthSettling { get; private set; }

    private int lowerMaxCandidateHealth;

    private int lowerMaxCandidateSamples;

    private bool lowerMaxCandidateRejected;

    private bool lowerMaxSettled;

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

    public static ObservedEnemyHealth FromSample(int currentHealth, bool isSpawnHealthSettling = false, int maxObservedHealthFloor = 0)
    {
        var normalizedHealth = NormalizeHealth(currentHealth);
        var normalizedMaxHealth = maxObservedHealthFloor > normalizedHealth ? maxObservedHealthFloor : normalizedHealth;
        return new ObservedEnemyHealth(normalizedHealth, normalizedMaxHealth, isSpawnHealthSettling && normalizedHealth > 0);
    }

    public void ApplySample(int currentHealth, bool allowSpawnSettleMaxDecrease = false, int strictMaxHealth = 0)
    {
        CurrentHealth = NormalizeHealth(currentHealth);
        SamplesSeen++;
        UpdateSpawnHealthSettling();

        if (CurrentHealth > MaxObservedHealth)
        {
            MaxObservedHealth = CurrentHealth;
            ClearLowerMaxCandidate();
        }
        else if (CanSettleMaxHealthDown(allowSpawnSettleMaxDecrease, strictMaxHealth))
        {
            ApplyLowerMaxCandidate(CurrentHealth);
        }
        else
        {
            ClearLowerMaxCandidate();
        }
    }

    public void ApplyAuthoritativeSample(int currentHealth, int maxHealth)
    {
        CurrentHealth = NormalizeHealth(currentHealth);
        SamplesSeen++;
        MaxObservedHealth = NormalizeHealth(maxHealth);
        if (MaxObservedHealth < CurrentHealth)
        {
            MaxObservedHealth = CurrentHealth;
        }

        lowerMaxSettled = true;
        lowerMaxCandidateRejected = false;
        IsSpawnHealthSettling = false;
        ClearLowerMaxCandidate();
    }

    private void UpdateSpawnHealthSettling()
    {
        if (!IsSpawnHealthSettling)
        {
            return;
        }

        if (SamplesSeen > InitialFullHealthSettleSampleLimit || CurrentHealth != MaxObservedHealth)
        {
            IsSpawnHealthSettling = false;
        }
    }

    private bool CanSettleMaxHealthDown(bool allowSpawnSettleMaxDecrease, int strictMaxHealth)
    {
        return allowSpawnSettleMaxDecrease &&
            SamplesSeen <= InitialFullHealthSettleSampleLimit &&
            (strictMaxHealth <= 0 || MaxObservedHealth <= strictMaxHealth) &&
            CurrentHealth > 0 &&
            !lowerMaxSettled &&
            !lowerMaxCandidateRejected &&
            CurrentHealth < MaxObservedHealth;
    }

    private void ApplyLowerMaxCandidate(int candidateHealth)
    {
        if (candidateHealth == lowerMaxCandidateHealth)
        {
            lowerMaxCandidateSamples++;
        }
        else
        {
            if (lowerMaxCandidateSamples > 0)
            {
                RejectLowerMaxCandidate();
                return;
            }

            StartLowerMaxCandidate(candidateHealth);
        }

        if (lowerMaxCandidateSamples >= LowerMaxConfirmationSamples)
        {
            MaxObservedHealth = candidateHealth;
            lowerMaxSettled = true;
            ClearLowerMaxCandidate();
        }
        else
        {
            IsMaxHealthSettling = true;
        }
    }

    private void StartLowerMaxCandidate(int candidateHealth)
    {
        lowerMaxCandidateHealth = candidateHealth;
        lowerMaxCandidateSamples = 1;
    }

    private void RejectLowerMaxCandidate()
    {
        ClearLowerMaxCandidate();
        lowerMaxCandidateRejected = true;
    }

    private void ClearLowerMaxCandidate()
    {
        lowerMaxCandidateHealth = 0;
        lowerMaxCandidateSamples = 0;
        IsMaxHealthSettling = false;
    }

    private static int NormalizeHealth(int health)
    {
        return health < 0 ? 0 : health;
    }
}
