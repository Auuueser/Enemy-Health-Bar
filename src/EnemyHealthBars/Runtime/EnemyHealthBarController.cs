using System.Collections.Generic;
using Auuueser.EnemyHealthBars.Configuration;
using Auuueser.EnemyHealthBars.Core.Domain;
using Auuueser.EnemyHealthBars.Game;
using Auuueser.EnemyHealthBars.Networking;
using Auuueser.EnemyHealthBars.Presentation;
using BepInEx.Logging;
using UnityEngine;

namespace Auuueser.EnemyHealthBars.Runtime;

internal sealed class EnemyHealthBarController : MonoBehaviour
{
    private const int MissingStateRetentionScans = 3;

    private readonly EnemyHealthTracker tracker = new();
    private readonly EnemyMaxHealthResolver maxHealthResolver = new();
    private readonly EnemyHealthSyncNetwork networkSync = new();
    private readonly HashSet<int> activeEnemyIds = new();
    private readonly HashSet<ulong> activeNetworkObjectIds = new();
    private readonly TransientIdRetentionSet<int> retainedEnemyStateIds = new(MissingStateRetentionScans);
    private readonly TransientIdRetentionSet<ulong> retainedNetworkStateIds = new(MissingStateRetentionScans);

    private ModConfig? config;
    private EnemySource? enemySource;
    private LocalPlayerCameraProvider? cameraProvider;
    private EnemyHealthBarPresenter? presenter;
    private EnemyHealthReader? healthReader;
    private EnemyScanDiagnostics? diagnostics;
    private float nextScanTime;
    private int lastSeenEnemyCount = -1;
    private int spawnScanFrame = -1;
    private int observedSettingsVersion;

    public void Initialize(
        ModConfig config,
        ManualLogSource logger,
        EnemySource enemySource,
        LocalPlayerCameraProvider cameraProvider,
        EnemyHealthBarPresenter presenter)
    {
        this.config = config;
        this.enemySource = enemySource;
        this.cameraProvider = cameraProvider;
        this.presenter = presenter;
        healthReader = new EnemyHealthReader(maxHealthResolver, EnemyHitPatches.Ledger);
        diagnostics = new EnemyScanDiagnostics(config, logger, maxHealthResolver, networkSync);
        observedSettingsVersion = config.SettingsVersion;
    }

    public void SetEnabled(bool enabled)
    {
        this.enabled = enabled;
        gameObject.SetActive(enabled);
    }

    private void Update()
    {
        if (config == null || cameraProvider == null || presenter == null || diagnostics == null)
        {
            return;
        }

        var currentTime = Time.unscaledTime;
        config.ReloadIfChangedOnDisk(currentTime);
        if (observedSettingsVersion != config.SettingsVersion)
        {
            observedSettingsVersion = config.SettingsVersion;
            presenter.RefreshStyle();
        }

        networkSync.Tick(config.Enabled && config.HostAuthoritySync, currentTime);

        if (!config.Enabled)
        {
            presenter.Clear();
            diagnostics.LogScanIfDue(EnemyScanCounts.Empty, null, "Disabled", currentTime);
            diagnostics.LogHeartbeatIfDue(null, presenter.ActiveCount, presenter.DebugTestBarVisible, currentTime);
            return;
        }

        var camera = cameraProvider.GetActiveCamera();
        if (camera == null)
        {
            presenter.Clear();
            diagnostics.LogScanIfDue(EnemyScanCounts.Empty, null, "No active camera", currentTime);
            diagnostics.LogHeartbeatIfDue(null, presenter.ActiveCount, presenter.DebugTestBarVisible, currentTime);
            return;
        }

        var billboardRotation = HealthBarBillboard.CalculateRotation(camera);
        UpdateDebugTestBar(camera, billboardRotation);
        diagnostics.LogHeartbeatIfDue(camera, presenter.ActiveCount, presenter.DebugTestBarVisible, currentTime);

        // A spawn or despawn changes the list size. The host lists a new enemy before its Start
        // runs (Start sets health, e.g. the single-player Butler), so scan on the next frame.
        var listedEnemyCount = enemySource != null ? enemySource.GetListedEnemyCount() : -1;
        if (listedEnemyCount != lastSeenEnemyCount)
        {
            lastSeenEnemyCount = listedEnemyCount;
            spawnScanFrame = Time.frameCount + 1;
        }

        var spawnScanDue = spawnScanFrame >= 0 && Time.frameCount >= spawnScanFrame;
        if (currentTime >= nextScanTime || spawnScanDue)
        {
            nextScanTime = currentTime + config.ScanInterval;
            spawnScanFrame = -1;
            RefreshEnemies(camera, billboardRotation, currentTime);
        }
        else
        {
            presenter.UpdateActive(camera, billboardRotation);
        }
    }

    private void OnDestroy()
    {
        networkSync.Dispose();
        presenter?.Clear();
    }

    private void RefreshEnemies(Camera camera, Quaternion billboardRotation, float currentTime)
    {
        if (config == null || enemySource == null || presenter == null || healthReader == null || diagnostics == null)
        {
            return;
        }

        activeEnemyIds.Clear();
        activeNetworkObjectIds.Clear();

        var enemies = enemySource.GetEnemies(currentTime);
        if (enemies == null)
        {
            var unavailableCounts = EnemyScanCounts.Empty;
            PruneMissingEnemyState(ref unavailableCounts);
            diagnostics.LogScanIfDue(unavailableCounts, camera, "SpawnedEnemies unavailable", currentTime);
            return;
        }

        var visibilityRules = config.CreateVisibilityRules();
        var cameraPosition = camera.transform.position;
        var counts = new EnemyScanCounts
        {
            Spawned = enemies.Count,
            Unlisted = enemySource.UnlistedEnemyCount,
        };
        diagnostics.BeginScan(currentTime);

        for (var i = 0; i < enemies.Count; i++)
        {
            var enemy = enemies[i];
            if (enemy == null)
            {
                counts.RejectedMissingEnemy++;
                diagnostics.AddMissingEnemy(i);
                continue;
            }

            if (!healthReader.TryRead(enemy, config.ShowInvulnerableEnemies, out var sample, out var readFailure))
            {
                counts.CountReadFailure(readFailure);
                diagnostics.AddReadFailure(enemy, readFailure);
                continue;
            }

            counts.Readable++;
            activeEnemyIds.Add(sample.EnemyId);
            if (sample.NetworkObjectId != 0UL)
            {
                activeNetworkObjectIds.Add(sample.NetworkObjectId);
            }

            var snapshot = tracker.Track(sample, config.MaxHealthMode);
            if (networkSync.TryGetHostSnapshot(sample, out var hostSnapshot))
            {
                snapshot = hostSnapshot;
                ApplyHostSyncedHealth(enemy, sample, snapshot);
                counts.UsedHostSync++;
            }
            else if (networkSync.ShouldSuppressLocalSnapshot(sample, currentTime))
            {
                counts.HiddenWaitingForHostSync++;
                diagnostics.AddSample(enemy, sample, snapshot, "hiddenWaitingForHostSync");
                presenter.Hide(sample.EnemyId);
                continue;
            }

            networkSync.QueueHostSnapshot(sample, snapshot);

            var visibility = visibilityRules.Classify(snapshot);
            if (visibility != HealthBarVisibility.Visible)
            {
                counts.CountHidden(visibility);
                diagnostics.AddSample(enemy, sample, snapshot, EnemyScanDiagnostics.DescribeHidden(visibility));
                presenter.Hide(sample.EnemyId);
                continue;
            }

            // The enemy root is close enough to its bar anchor for distance gating, and it avoids
            // resolving renderer bounds for enemies that are out of range.
            var squaredDistanceToCamera = (cameraPosition - enemy.transform.position).sqrMagnitude;
            if (!visibilityRules.IsWithinSquaredDistance(squaredDistanceToCamera))
            {
                counts.HiddenDistance++;
                diagnostics.AddSample(enemy, sample, snapshot, "hiddenDistance");
                presenter.Hide(sample.EnemyId);
                continue;
            }

            counts.Shown++;
            diagnostics.AddSample(enemy, sample, snapshot, "shown");
            presenter.ShowOrUpdate(enemy, snapshot, camera, billboardRotation);
        }

        PruneMissingEnemyState(ref counts);
        counts.Active = presenter.ActiveCount;
        diagnostics.LogScanIfDue(counts, camera, EnemyScanDiagnostics.ScanSource, currentTime);
    }

    private void PruneMissingEnemyState(ref EnemyScanCounts counts)
    {
        var retainedEnemyIds = retainedEnemyStateIds.Update(activeEnemyIds);
        var retainedNetworkObjectIds = retainedNetworkStateIds.Update(activeNetworkObjectIds);
        tracker.KeepOnly(retainedEnemyIds);
        EnemyHitPatches.Ledger.KeepOnly(retainedEnemyIds);
        EnemyHealthOverrideStore.KeepOnly(retainedEnemyIds, retainedNetworkObjectIds);
        networkSync.KeepOnly(retainedNetworkObjectIds);
        networkSync.FlushHostSnapshots();
        presenter?.HideMissing(activeEnemyIds);
        counts.RetainedEnemyStates = retainedEnemyIds.Count - activeEnemyIds.Count;
        counts.RetainedNetworkStates = retainedNetworkObjectIds.Count - activeNetworkObjectIds.Count;
    }

    private void ApplyHostSyncedHealth(EnemyAI enemy, EnemyHealthSample sample, EnemyHealthSnapshot snapshot)
    {
        if (snapshot.IsDead || snapshot.CurrentHealth <= 0 || enemy.isEnemyDead || EnemyMaxHealthResolver.IsOneHitKill(enemy))
        {
            return;
        }

        // Snapshots carry effective health; write back the raw enemyHP the game uses.
        var enemyHP = snapshot.CurrentHealth - EnemyMaxHealthResolver.GetHealthOffset(enemy);
        if (enemy.enemyHP != enemyHP)
        {
            enemy.enemyHP = enemyHP;
        }

        networkSync.ConfirmHostSnapshotApplied(sample, snapshot);
    }

    private void UpdateDebugTestBar(Camera camera, Quaternion billboardRotation)
    {
        if (config == null || presenter == null)
        {
            return;
        }

        if (config.DebugShowTestBar)
        {
            presenter.ShowDebugTestBar(camera, billboardRotation);
        }
        else
        {
            presenter.HideDebugTestBar();
        }
    }
}
