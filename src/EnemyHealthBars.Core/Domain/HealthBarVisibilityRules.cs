namespace Auuueser.EnemyHealthBars.Core.Domain;

public enum HealthBarVisibility
{
    Visible,
    HiddenDead,
    HiddenMaxHealthSettling,
    HiddenZeroHealth,
    HiddenFullHealth,
}

public readonly struct HealthBarVisibilityRules
{
    private readonly bool showWhenFullHealth;
    private readonly float maxDistance;
    private readonly float maxDistanceSquared;

    public HealthBarVisibilityRules(bool showWhenFullHealth, float maxDistance)
    {
        this.showWhenFullHealth = showWhenFullHealth;
        this.maxDistance = maxDistance;
        maxDistanceSquared = maxDistance > 0f ? maxDistance * maxDistance : 0f;
    }

    // Spawn settling is not a gate: a fresh enemy shows its bar at once and the max updates
    // if a mod changes its health right after spawning.
    public HealthBarVisibility Classify(EnemyHealthSnapshot snapshot)
    {
        if (snapshot.IsDead)
        {
            return HealthBarVisibility.HiddenDead;
        }

        if (snapshot.IsMaxHealthSettling)
        {
            return HealthBarVisibility.HiddenMaxHealthSettling;
        }

        if (snapshot.CurrentHealth <= 0 || snapshot.MaxHealth <= 0)
        {
            return HealthBarVisibility.HiddenZeroHealth;
        }

        return !showWhenFullHealth && snapshot.IsFullHealth
            ? HealthBarVisibility.HiddenFullHealth
            : HealthBarVisibility.Visible;
    }

    public bool ShouldShow(EnemyHealthSnapshot snapshot, float distanceToCamera)
    {
        return ShouldShowBySquaredDistance(snapshot, distanceToCamera * distanceToCamera);
    }

    public bool ShouldShowBySquaredDistance(EnemyHealthSnapshot snapshot, float squaredDistanceToCamera)
    {
        return Classify(snapshot) == HealthBarVisibility.Visible && IsWithinSquaredDistance(squaredDistanceToCamera);
    }

    public bool IsWithinSquaredDistance(float squaredDistanceToCamera)
    {
        return maxDistance <= 0f || squaredDistanceToCamera <= maxDistanceSquared;
    }
}
