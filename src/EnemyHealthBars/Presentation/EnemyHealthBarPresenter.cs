using System.Collections.Generic;
using Auuueser.EnemyHealthBars.Configuration;
using Auuueser.EnemyHealthBars.Core.Configuration;
using Auuueser.EnemyHealthBars.Core.Domain;
using UnityEngine;

namespace Auuueser.EnemyHealthBars.Presentation;

internal sealed class EnemyHealthBarPresenter
{
    private const float TargetPositionRefreshInterval = 0.1f;
    private const int PrewarmedViewCount = 4;

    private readonly ModConfig config;
    private readonly HealthBarPool pool;
    private readonly HealthBarTargetResolver targetResolver = new();
    private readonly Dictionary<int, ActiveHealthBar> activeBars = new();
    private readonly List<int> removeBuffer = new();
    private HealthBarView? debugTestBar;

    public EnemyHealthBarPresenter(ModConfig config, Transform parent)
    {
        this.config = config;
        pool = new HealthBarPool(config, parent);
        pool.Prewarm(PrewarmedViewCount);
    }

    public int ActiveCount => activeBars.Count;

    public bool DebugTestBarVisible => debugTestBar != null;

    public void ShowOrUpdate(EnemyAI enemy, EnemyHealthSnapshot snapshot, Vector3 worldPosition, Camera camera, Quaternion billboardRotation)
    {
        if (!activeBars.TryGetValue(snapshot.EnemyId, out var activeBar))
        {
            activeBar = new ActiveHealthBar(enemy, pool.Get());
            activeBars.Add(snapshot.EnemyId, activeBar);
        }

        activeBar.Enemy = enemy;
        activeBar.Snapshot = snapshot;
        activeBar.RefreshPosition(worldPosition, config.VerticalOffset, config.DisplayMode, Time.unscaledTime);
        activeBar.View.SetHealth(snapshot);
        activeBar.View.SetWorldPosition(worldPosition, camera, billboardRotation);
        activeBar.View.SetVisible(true);
    }

    public void Hide(int enemyId)
    {
        if (!activeBars.TryGetValue(enemyId, out var activeBar))
        {
            return;
        }

        activeBars.Remove(enemyId);
        pool.Release(activeBar.View);
    }

    public void HideMissing(ISet<int> activeEnemyIds)
    {
        removeBuffer.Clear();

        foreach (var pair in activeBars)
        {
            if (!activeEnemyIds.Contains(pair.Key))
            {
                removeBuffer.Add(pair.Key);
            }
        }

        foreach (var enemyId in removeBuffer)
        {
            Hide(enemyId);
        }
    }

    public void RefreshStyle()
    {
        var style = HealthBarStyle.FromConfig(config);
        pool.RefreshStyle(style);
        debugTestBar?.ApplyStyle(style);

        foreach (var pair in activeBars)
        {
            pair.Value.View.ApplyStyle(style);
            pair.Value.View.SetHealth(pair.Value.Snapshot);
        }
    }

    public void ShowDebugTestBar(Camera camera, Quaternion billboardRotation)
    {
        if (debugTestBar == null)
        {
            debugTestBar = pool.Get();
            debugTestBar.SetVisible(true);
        }

        var worldPosition = camera.transform.position +
            camera.transform.forward * 2.25f -
            camera.transform.up * 0.35f;

        debugTestBar.SetVisible(true);
        debugTestBar.SetHealth(new EnemyHealthSnapshot(
            EnemyId: -1,
            DisplayName: "Test enemy",
            CurrentHealth: 13,
            MaxHealth: 20,
            IsDead: false));
        debugTestBar.SetWorldPosition(worldPosition, camera, billboardRotation);
    }

    public void HideDebugTestBar()
    {
        if (debugTestBar == null)
        {
            return;
        }

        pool.Release(debugTestBar);
        debugTestBar = null;
    }

    public void UpdateActive(Camera camera, Quaternion billboardRotation)
    {
        removeBuffer.Clear();

        foreach (var pair in activeBars)
        {
            var activeBar = pair.Value;
            if (activeBar.Enemy == null)
            {
                removeBuffer.Add(pair.Key);
                continue;
            }

            var worldPosition = activeBar.GetWorldPosition(
                targetResolver,
                config.VerticalOffset,
                config.DisplayMode,
                Time.unscaledTime);
            activeBar.View.SetWorldPosition(worldPosition, camera, billboardRotation);
        }

        foreach (var enemyId in removeBuffer)
        {
            Hide(enemyId);
        }
    }

    public void Clear()
    {
        HideDebugTestBar();
        removeBuffer.Clear();
        foreach (var enemyId in activeBars.Keys)
        {
            removeBuffer.Add(enemyId);
        }

        foreach (var enemyId in removeBuffer)
        {
            Hide(enemyId);
        }
    }

    public Vector3 GetWorldPosition(EnemyAI enemy)
    {
        return targetResolver.GetWorldPosition(enemy, config.VerticalOffset, config.DisplayMode);
    }

    private sealed class ActiveHealthBar
    {
        private Transform? cachedTransform;
        private Vector3 cachedLocalPosition;
        private Vector3 cachedWorldPosition;
        private HealthBarDisplayMode cachedDisplayMode = (HealthBarDisplayMode)(-1);
        private float cachedVerticalOffset;
        private float nextTargetRefreshTime;
        private bool hasCachedPosition;

        public ActiveHealthBar(EnemyAI enemy, HealthBarView view)
        {
            Enemy = enemy;
            View = view;
        }

        public EnemyAI Enemy { get; set; }

        public EnemyHealthSnapshot Snapshot { get; set; }

        public HealthBarView View { get; }

        public void RefreshPosition(
            Vector3 worldPosition,
            float verticalOffset,
            HealthBarDisplayMode displayMode,
            float currentTime)
        {
            var followTransform = ResolveFollowTransform(Enemy);
            cachedTransform = followTransform;
            cachedWorldPosition = worldPosition;
            cachedLocalPosition = followTransform != null ? followTransform.InverseTransformPoint(worldPosition) : worldPosition;
            cachedVerticalOffset = verticalOffset;
            cachedDisplayMode = displayMode;
            nextTargetRefreshTime = currentTime + TargetPositionRefreshInterval;
            hasCachedPosition = true;
        }

        public Vector3 GetWorldPosition(
            HealthBarTargetResolver targetResolver,
            float verticalOffset,
            HealthBarDisplayMode displayMode,
            float currentTime)
        {
            if (!hasCachedPosition ||
                cachedDisplayMode != displayMode ||
                cachedVerticalOffset != verticalOffset ||
                currentTime >= nextTargetRefreshTime)
            {
                var resolvedPosition = targetResolver.GetWorldPosition(Enemy, verticalOffset, displayMode);
                RefreshPosition(resolvedPosition, verticalOffset, displayMode, currentTime);
                return resolvedPosition;
            }

            if (cachedTransform == null)
            {
                var resolvedPosition = targetResolver.GetWorldPosition(Enemy, verticalOffset, displayMode);
                RefreshPosition(resolvedPosition, verticalOffset, displayMode, currentTime);
                return resolvedPosition;
            }

            cachedWorldPosition = cachedTransform.TransformPoint(cachedLocalPosition);
            return cachedWorldPosition;
        }

        private static Transform? ResolveFollowTransform(EnemyAI enemy)
        {
            if (enemy == null)
            {
                return null;
            }

            if (enemy is MaskedPlayerEnemy)
            {
                var head = enemy.GetRadarHeadTransform();
                if (head != null)
                {
                    return head;
                }
            }

            return enemy.transform;
        }
    }
}
