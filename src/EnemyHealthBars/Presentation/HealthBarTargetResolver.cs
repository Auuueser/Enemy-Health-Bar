using Auuueser.EnemyHealthBars.Core.Configuration;
using UnityEngine;

namespace Auuueser.EnemyHealthBars.Presentation;

internal sealed class HealthBarTargetResolver
{
    public Vector3 GetWorldPosition(EnemyAI enemy, float verticalOffset, HealthBarDisplayMode displayMode)
    {
        if (displayMode == HealthBarDisplayMode.VerticalSideBar)
        {
            return GetVerticalSideWorldPosition(enemy, verticalOffset);
        }

        if (TryGetMaskedStableAnchor(enemy, out var maskedAnchor))
        {
            return maskedAnchor + Vector3.up * verticalOffset;
        }

        if (TryGetRendererBounds(enemy, out var bounds))
        {
            var anchorY = enemy.eye != null ? Mathf.Max(enemy.eye.position.y, bounds.max.y) : bounds.max.y;
            return new Vector3(bounds.center.x, anchorY + verticalOffset, bounds.center.z);
        }

        if (enemy.eye != null)
        {
            return enemy.eye.position + Vector3.up * verticalOffset;
        }

        return enemy.transform.position + Vector3.up * (1.5f + verticalOffset);
    }

    private static Vector3 GetVerticalSideWorldPosition(EnemyAI enemy, float verticalOffset)
    {
        var sideOffset = verticalOffset * 0.25f;
        if (TryGetMaskedStableAnchor(enemy, out var maskedAnchor))
        {
            return maskedAnchor + Vector3.up * (sideOffset - 0.35f);
        }

        if (TryGetRendererBounds(enemy, out var bounds))
        {
            return new Vector3(bounds.center.x, bounds.center.y + sideOffset, bounds.center.z);
        }

        if (enemy.eye != null)
        {
            return enemy.eye.position + Vector3.up * (sideOffset - 0.35f);
        }

        return enemy.transform.position + Vector3.up * (1f + sideOffset);
    }

    private static bool TryGetMaskedStableAnchor(EnemyAI enemy, out Vector3 anchor)
    {
        anchor = default;
        if (enemy is not MaskedPlayerEnemy)
        {
            return false;
        }

        var head = enemy.GetRadarHeadTransform();
        if (head == null)
        {
            return false;
        }

        anchor = head.position;
        return true;
    }

    private static bool TryGetRendererBounds(EnemyAI enemy, out Bounds bounds)
    {
        bounds = default;
        var hasBounds = false;

        if (enemy.skinnedMeshRenderers != null)
        {
            foreach (var renderer in enemy.skinnedMeshRenderers)
            {
                hasBounds = EncapsulateRendererBounds(renderer, ref bounds, hasBounds);
            }
        }

        if (enemy.meshRenderers != null)
        {
            foreach (var renderer in enemy.meshRenderers)
            {
                hasBounds = EncapsulateRendererBounds(renderer, ref bounds, hasBounds);
            }
        }

        return hasBounds;
    }

    private static bool EncapsulateRendererBounds(Renderer? renderer, ref Bounds bounds, bool hasBounds)
    {
        if (renderer == null)
        {
            return hasBounds;
        }

        if (!hasBounds)
        {
            bounds = renderer.bounds;
            return true;
        }

        bounds.Encapsulate(renderer.bounds);
        return true;
    }
}
