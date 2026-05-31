namespace Auuueser.EnemyHealthBars.Core.Domain;

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

    public bool ShouldShow(EnemyHealthSnapshot snapshot, float distanceToCamera)
    {
        if (snapshot.IsDead || snapshot.CurrentHealth <= 0 || snapshot.MaxHealth <= 0)
        {
            return false;
        }

        if (!showWhenFullHealth && snapshot.IsFullHealth)
        {
            return false;
        }

        return IsWithinSquaredDistance(distanceToCamera * distanceToCamera);
    }

    public bool ShouldShowBySquaredDistance(EnemyHealthSnapshot snapshot, float squaredDistanceToCamera)
    {
        if (snapshot.IsDead || snapshot.CurrentHealth <= 0 || snapshot.MaxHealth <= 0)
        {
            return false;
        }

        if (!showWhenFullHealth && snapshot.IsFullHealth)
        {
            return false;
        }

        return IsWithinSquaredDistance(squaredDistanceToCamera);
    }

    public bool IsWithinSquaredDistance(float squaredDistanceToCamera)
    {
        return maxDistance <= 0f || squaredDistanceToCamera <= maxDistanceSquared;
    }
}
