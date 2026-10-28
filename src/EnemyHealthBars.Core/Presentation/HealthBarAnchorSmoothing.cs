using System;

namespace Auuueser.EnemyHealthBars.Core.Presentation;

// Frame-rate independent smoothing of a bar's offset from its enemy's root, on top of the
// measured anchor (renderer top / eye / Masked head). Horizontal offsets adapt slowly outside a
// dead band, so limb swing does not shake the bar sideways. Heights rise fast (popped heads,
// flights) and fall gently, which filters walk bob without trailing the body.
public static class HealthBarAnchorSmoothing
{
    public const float HorizontalFollowRate = 1.5f;

    public const float HorizontalDeadBand = 0.35f;

    public const float RiseRate = 8f;

    public const float FallRate = 2f;

    public const float FallDeadBand = 0.08f;

    public const float MaxStepSeconds = 0.25f;

    public static float Follow(float current, float target, float deltaTime, float followRate, float deadBand = 0f)
    {
        var delta = target - current;
        if (deltaTime <= 0f || (delta <= deadBand && delta >= -deadBand))
        {
            return current;
        }

        if (deltaTime > MaxStepSeconds)
        {
            deltaTime = MaxStepSeconds;
        }

        var blend = 1f - (float)Math.Exp(-followRate * deltaTime);
        return current + delta * blend;
    }

    public static float FollowHeight(float current, float target, float deltaTime)
    {
        return target > current
            ? Follow(current, target, deltaTime, RiseRate)
            : Follow(current, target, deltaTime, FallRate, FallDeadBand);
    }

    public static float FollowHorizontal(float current, float target, float deltaTime)
    {
        return Follow(current, target, deltaTime, HorizontalFollowRate, HorizontalDeadBand);
    }
}
