namespace Auuueser.EnemyHealthBars.Core.Presentation;

public static class HealthBarSegments
{
    public const int MaxSegments = 12;

    public const float LowHealthFraction = 1f / 3f;

    private const float LowHealthTolerance = 0.001f;

    // One segment per health point while that stays readable; larger pools use a continuous bar.
    public static int ResolveSegmentCount(int maxHealth)
    {
        return maxHealth > 0 && maxHealth <= MaxSegments ? maxHealth : 0;
    }

    public static int ResolveFilledSegments(int currentHealth, int segmentCount)
    {
        if (currentHealth <= 0)
        {
            return 0;
        }

        return currentHealth > segmentCount ? segmentCount : currentHealth;
    }

    public static bool IsLowHealth(int currentHealth, float healthFraction)
    {
        if (currentHealth <= 0 || healthFraction <= 0f)
        {
            return false;
        }

        return currentHealth == 1 || healthFraction <= LowHealthFraction + LowHealthTolerance;
    }
}

/// <summary>
/// The pale "just lost" section of a bar: it holds briefly after a hit, then drains to the new health.
/// </summary>
public struct DamageTrail
{
    public const float HoldSeconds = 0.15f;
    public const float DrainSeconds = 0.35f;

    private float startTime;

    public bool IsActive { get; private set; }

    public float FromFraction { get; private set; }

    public void OnHealthChanged(float previousFraction, float currentFraction, float currentTime)
    {
        if (previousFraction < 0f || currentFraction >= previousFraction)
        {
            IsActive = false;
            return;
        }

        var visibleTop = IsActive ? Evaluate(currentTime, currentFraction, out _) : previousFraction;
        FromFraction = visibleTop > previousFraction ? visibleTop : previousFraction;
        startTime = currentTime;
        IsActive = true;
    }

    public float Evaluate(float currentTime, float currentFraction, out float strength)
    {
        if (!IsActive)
        {
            strength = 0f;
            return currentFraction;
        }

        var elapsed = currentTime - startTime;
        if (elapsed < HoldSeconds)
        {
            strength = 1f;
            return FromFraction;
        }

        var progress = (elapsed - HoldSeconds) / DrainSeconds;
        if (progress >= 1f)
        {
            IsActive = false;
            strength = 0f;
            return currentFraction;
        }

        strength = 1f - progress;
        return FromFraction + (currentFraction - FromFraction) * progress;
    }

    public void Clear()
    {
        IsActive = false;
    }
}
