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

    private bool hitObserved;

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

    public static ObservedEnemyHealth FromSample(
        int currentHealth,
        bool isSpawnHealthSettling = false,
        int maxObservedHealthFloor = 0,
        int healthBeforeFirstHit = 0)
    {
        var normalizedHealth = NormalizeHealth(currentHealth);
        if (healthBeforeFirstHit > maxObservedHealthFloor)
        {
            maxObservedHealthFloor = healthBeforeFirstHit;
        }

        var normalizedMaxHealth = maxObservedHealthFloor > normalizedHealth ? maxObservedHealthFloor : normalizedHealth;
        var wasHit = healthBeforeFirstHit > 0;
        var observedHealth = new ObservedEnemyHealth(normalizedHealth, normalizedMaxHealth, isSpawnHealthSettling && normalizedHealth > 0 && !wasHit);
        if (wasHit)
        {
            observedHealth.hitObserved = true;
            observedHealth.lowerMaxSettled = true;
        }

        return observedHealth;
    }

    public void ApplySample(
        int currentHealth,
        bool allowSpawnSettleMaxDecrease = false,
        int strictMaxHealth = 0,
        int healthBeforeFirstHit = 0)
    {
        CurrentHealth = NormalizeHealth(currentHealth);
        SamplesSeen++;
        if (healthBeforeFirstHit > 0 && !hitObserved)
        {
            ApplyFirstHit(healthBeforeFirstHit, allowSpawnSettleMaxDecrease, strictMaxHealth);
        }

        UpdateSpawnHealthSettling();

        if (CurrentHealth > MaxObservedHealth)
        {
            MaxObservedHealth = CurrentHealth;
            ClearLowerMaxCandidate();
        }
        else if (IsEarlyDropToStrictMax(allowSpawnSettleMaxDecrease, strictMaxHealth))
        {
            // Spawn code set the vanilla value after the first sample (prefab 8 -> Butler 2).
            MaxObservedHealth = CurrentHealth;
            lowerMaxSettled = true;
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

    private void ApplyFirstHit(int healthBeforeFirstHit, bool allowSpawnSettleMaxDecrease, int strictMaxHealth)
    {
        // Nothing damaged the enemy before its first hit, so the health right before that hit is its spawn health.
        if (allowSpawnSettleMaxDecrease &&
            !lowerMaxSettled &&
            SamplesSeen <= InitialFullHealthSettleSampleLimit &&
            (strictMaxHealth <= 0 || MaxObservedHealth <= strictMaxHealth))
        {
            MaxObservedHealth = healthBeforeFirstHit;
        }
        else if (healthBeforeFirstHit > MaxObservedHealth)
        {
            MaxObservedHealth = healthBeforeFirstHit;
        }

        if (MaxObservedHealth < CurrentHealth)
        {
            MaxObservedHealth = CurrentHealth;
        }

        hitObserved = true;
        lowerMaxSettled = true;
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

    private bool IsEarlyDropToStrictMax(bool allowSpawnSettleMaxDecrease, int strictMaxHealth)
    {
        return allowSpawnSettleMaxDecrease &&
            strictMaxHealth > 0 &&
            CurrentHealth == strictMaxHealth &&
            MaxObservedHealth > strictMaxHealth &&
            SamplesSeen <= InitialFullHealthSettleSampleLimit &&
            !hitObserved &&
            !lowerMaxSettled;
    }

    private bool CanSettleMaxHealthDown(bool allowSpawnSettleMaxDecrease, int strictMaxHealth)
    {
        return allowSpawnSettleMaxDecrease &&
            SamplesSeen <= InitialFullHealthSettleSampleLimit &&
            (strictMaxHealth <= 0 || MaxObservedHealth <= strictMaxHealth) &&
            CurrentHealth > 0 &&
            !hitObserved &&
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
