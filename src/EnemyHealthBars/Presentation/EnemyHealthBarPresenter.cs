using System.Collections.Generic;
using Auuueser.EnemyHealthBars.Configuration;
using Auuueser.EnemyHealthBars.Core.Configuration;
using Auuueser.EnemyHealthBars.Core.Domain;
using Auuueser.EnemyHealthBars.Core.Presentation;
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
    private readonly Stack<ActiveHealthBar> freeBars = new();
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

    public void ShowOrUpdate(EnemyAI enemy, EnemyHealthSnapshot snapshot, Camera camera, Quaternion billboardRotation)
    {
        if (!activeBars.TryGetValue(snapshot.EnemyId, out var activeBar))
        {
            activeBar = freeBars.Count > 0 ? freeBars.Pop() : new ActiveHealthBar();
            activeBar.Attach(enemy, pool.Get(), warmUp: true);
            activeBars.Add(snapshot.EnemyId, activeBar);
        }
        else if (activeBar.Enemy != enemy)
        {
            activeBar.Attach(enemy, activeBar.View, warmUp: true);
        }

        activeBar.Snapshot = snapshot;
        var worldPosition = activeBar.GetWorldPosition(
            targetResolver,
            config.VerticalOffset,
            config.DisplayMode,
            Time.unscaledTime);
        activeBar.View.SetHealth(snapshot);
        activeBar.View.SetWorldPosition(worldPosition, camera, billboardRotation);
        activeBar.View.SetVisible(activeBar.IsWarm);
    }

    public void Hide(int enemyId)
    {
        if (!activeBars.TryGetValue(enemyId, out var activeBar))
        {
            return;
        }

        activeBars.Remove(enemyId);
        pool.Release(activeBar.View);
        activeBar.Detach();
        freeBars.Push(activeBar);
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
        var currentTime = Time.unscaledTime;

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
                currentTime);
            activeBar.View.SetWorldPosition(worldPosition, camera, billboardRotation);
            activeBar.View.SetVisible(activeBar.IsWarm);
            activeBar.View.Tick(currentTime);
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

    // Bars follow the enemy root every frame at a smoothed offset toward the measured anchor
    // (re-measured at a low rate). A newly attached bar stays hidden for a few frames so its first
    // measurement comes after the enemy's animator has posed it, instead of a taller spawn pose.
    private sealed class ActiveHealthBar
    {
        private const int WarmupFrames = 3;

        private Transform? rootTransform;
        private Vector3 offset;
        private Vector3 targetOffset;
        private float lastFollowTime;
        private int warmupEndFrame;
        private HealthBarDisplayMode cachedDisplayMode = (HealthBarDisplayMode)(-1);
        private float cachedVerticalOffset;
        private float nextTargetRefreshTime;
        private bool hasCachedPosition;

        public EnemyAI Enemy { get; private set; } = null!;

        public EnemyHealthSnapshot Snapshot { get; set; }

        public HealthBarView View { get; private set; } = null!;

        public bool IsWarm => Time.frameCount >= warmupEndFrame;

        public void Attach(EnemyAI enemy, HealthBarView view, bool warmUp)
        {
            Enemy = enemy;
            View = view;
            rootTransform = null;
            warmupEndFrame = warmUp ? Time.frameCount + WarmupFrames : 0;
            hasCachedPosition = false;
        }

        public void Detach()
        {
            Enemy = null!;
            View = null!;
            rootTransform = null;
            hasCachedPosition = false;
        }

        public Vector3 GetWorldPosition(
            HealthBarTargetResolver targetResolver,
            float verticalOffset,
            HealthBarDisplayMode displayMode,
            float currentTime)
        {
            var settingsChanged = cachedDisplayMode != displayMode || cachedVerticalOffset != verticalOffset;
            var snap = !hasCachedPosition || settingsChanged || rootTransform == null || !IsWarm;
            if (snap || currentTime >= nextTargetRefreshTime)
            {
                RefreshAnchor(targetResolver, verticalOffset, displayMode, currentTime, snap);
            }

            if (rootTransform == null)
            {
                // The enemy was destroyed; UpdateActive removes this bar on the same pass.
                return Vector3.zero;
            }

            var deltaTime = currentTime - lastFollowTime;
            lastFollowTime = currentTime;
            offset = new Vector3(
                HealthBarAnchorSmoothing.FollowHorizontal(offset.x, targetOffset.x, deltaTime),
                HealthBarAnchorSmoothing.FollowHeight(offset.y, targetOffset.y, deltaTime),
                HealthBarAnchorSmoothing.FollowHorizontal(offset.z, targetOffset.z, deltaTime));

            var horizontal = YawRotation(rootTransform) * new Vector3(offset.x, 0f, offset.z);
            return rootTransform.position + horizontal + Vector3.up * offset.y;
        }

        private void RefreshAnchor(
            HealthBarTargetResolver targetResolver,
            float verticalOffset,
            HealthBarDisplayMode displayMode,
            float currentTime,
            bool snap)
        {
            var enemy = Enemy;
            rootTransform = enemy != null ? enemy.transform : null;
            cachedVerticalOffset = verticalOffset;
            cachedDisplayMode = displayMode;
            nextTargetRefreshTime = currentTime + TargetPositionRefreshInterval;
            if (enemy == null || rootTransform == null)
            {
                hasCachedPosition = false;
                return;
            }

            var measured = targetResolver.GetWorldPosition(enemy, verticalOffset, displayMode);
            var delta = measured - rootTransform.position;
            var horizontal = Quaternion.Inverse(YawRotation(rootTransform)) * new Vector3(delta.x, 0f, delta.z);
            targetOffset = new Vector3(horizontal.x, delta.y, horizontal.z);
            if (snap)
            {
                offset = targetOffset;
                lastFollowTime = currentTime;
            }

            hasCachedPosition = true;
        }

        private static Quaternion YawRotation(Transform transform)
        {
            return Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
        }
    }
}
