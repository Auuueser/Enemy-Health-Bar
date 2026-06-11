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
    private const int MaxSampleDiagnosticsPerScan = 8;
    private const int MissingStateRetentionScans = 3;

    private readonly EnemyHealthTracker tracker = new();
    private readonly EnemyHealthReader healthReader = new();
    private readonly EnemyMaxHealthResolver maxHealthResolver = new();
    private readonly EnemyHealthSyncNetwork networkSync = new();
    private readonly HashSet<int> activeEnemyIds = new();
    private readonly HashSet<ulong> activeNetworkObjectIds = new();
    private readonly TransientIdRetentionSet<int> retainedEnemyStateIds = new(MissingStateRetentionScans);
    private readonly TransientIdRetentionSet<ulong> retainedNetworkStateIds = new(MissingStateRetentionScans);

    private ModConfig? config;
    private ManualLogSource? logger;
    private RoundManagerEnemySource? enemySource;
    private LocalPlayerCameraProvider? cameraProvider;
    private EnemyHealthBarPresenter? presenter;
    private float nextScanTime;
    private float nextDiagnosticLogTime;
    private float nextDebugHeartbeatLogTime;
    private int observedSettingsVersion;

    public void Initialize(
        ModConfig config,
        ManualLogSource logger,
        RoundManagerEnemySource enemySource,
        LocalPlayerCameraProvider cameraProvider,
        EnemyHealthBarPresenter presenter)
    {
        this.config = config;
        this.logger = logger;
        this.enemySource = enemySource;
        this.cameraProvider = cameraProvider;
        this.presenter = presenter;
        observedSettingsVersion = config.SettingsVersion;
    }

    public void SetEnabled(bool enabled)
    {
        this.enabled = enabled;
        gameObject.SetActive(enabled);
    }

    private void Update()
    {
        if (config == null || enemySource == null || cameraProvider == null || presenter == null)
        {
            return;
        }

        config.ReloadIfChangedOnDisk(Time.unscaledTime);
        if (observedSettingsVersion != config.SettingsVersion)
        {
            observedSettingsVersion = config.SettingsVersion;
            presenter.RefreshStyle();
        }

        networkSync.Tick(config.Enabled && config.HostAuthoritySync, Time.unscaledTime);

        if (!config.Enabled)
        {
            presenter.Clear();
            LogDiagnosticsIfDue(DiagnosticScanCounts.Empty, null, "Disabled");
            LogDebugHeartbeatIfDue(null);
            return;
        }

        var camera = cameraProvider.GetActiveCamera();
        if (camera == null)
        {
            presenter.Clear();
            LogDiagnosticsIfDue(DiagnosticScanCounts.Empty, null, "No active camera");
            LogDebugHeartbeatIfDue(null);
            return;
        }

        var billboardRotation = HealthBarBillboard.CalculateRotation(camera);
        UpdateDebugTestBar(camera, billboardRotation);
        LogDebugHeartbeatIfDue(camera);

        var scanRefreshed = false;
        if (Time.unscaledTime >= nextScanTime)
        {
            nextScanTime = Time.unscaledTime + config.ScanInterval;
            RefreshEnemies(camera, billboardRotation);
            scanRefreshed = true;
        }

        if (!scanRefreshed)
        {
            presenter.UpdateActive(camera, billboardRotation);
        }
    }

    private void OnDestroy()
    {
        networkSync.Dispose();
        presenter?.Clear();
    }

    private void RefreshEnemies(Camera camera, Quaternion billboardRotation)
    {
        if (config == null || enemySource == null || presenter == null)
        {
            return;
        }

        activeEnemyIds.Clear();
        activeNetworkObjectIds.Clear();
        var visibilityRules = config.CreateVisibilityRules();
        var cameraPosition = camera.transform.position;

        var spawnedEnemies = enemySource.GetSpawnedEnemies();
        if (spawnedEnemies == null)
        {
            var retainedEnemyIds = retainedEnemyStateIds.Update(activeEnemyIds);
            var retainedNetworkObjectIds = retainedNetworkStateIds.Update(activeNetworkObjectIds);
            tracker.KeepOnly(retainedEnemyIds);
            EnemyHealthOverrideStore.KeepOnly(retainedEnemyIds, retainedNetworkObjectIds);
            networkSync.KeepOnlyClientSnapshots(retainedNetworkObjectIds);
            networkSync.KeepOnlyHostSnapshots(retainedNetworkObjectIds);
            networkSync.FlushHostSnapshots();
            presenter.HideMissing(activeEnemyIds);
            LogDiagnosticsIfDue(DiagnosticScanCounts.Empty, camera, "SpawnedEnemies unavailable");
            return;
        }

        var counts = new DiagnosticScanCounts
        {
            Spawned = spawnedEnemies.Count,
        };
        var sampleDiagnostics = ShouldCollectSampleDiagnostics() ? new List<string>(MaxSampleDiagnosticsPerScan) : null;

        for (var i = 0; i < spawnedEnemies.Count; i++)
        {
            var enemy = spawnedEnemies[i];
            if (enemy == null)
            {
                counts.RejectedMissingEnemy++;
                AddMissingEnemyDiagnostic(sampleDiagnostics, i);
                continue;
            }

            if (!healthReader.TryRead(enemy, config.ShowInvulnerableEnemies, maxHealthResolver, out var sample, out var readFailure))
            {
                AddReadFailure(ref counts, readFailure);
                AddReadFailureDiagnostic(sampleDiagnostics, enemy, readFailure);
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
            else if (networkSync.ShouldSuppressLocalSnapshot(sample, Time.unscaledTime))
            {
                counts.HiddenWaitingForHostSync++;
                AddSampleDiagnostic(sampleDiagnostics, enemy, sample, snapshot, "hiddenWaitingForHostSync");
                presenter.Hide(sample.EnemyId);
                continue;
            }

            networkSync.QueueHostSnapshot(sample, snapshot);

            if (snapshot.IsDead)
            {
                counts.HiddenDead++;
                AddSampleDiagnostic(sampleDiagnostics, enemy, sample, snapshot, "hiddenDead");
                presenter.Hide(sample.EnemyId);
            }
            else if (snapshot.IsMaxHealthSettling)
            {
                counts.HiddenSettling++;
                AddSampleDiagnostic(sampleDiagnostics, enemy, sample, snapshot, "hiddenSettling");
                presenter.Hide(sample.EnemyId);
            }
            else if (snapshot.IsSpawnHealthSettling)
            {
                counts.HiddenSpawnSettling++;
                AddSampleDiagnostic(sampleDiagnostics, enemy, sample, snapshot, "hiddenSpawnSettling");
                presenter.Hide(sample.EnemyId);
            }
            else if (snapshot.CurrentHealth <= 0 || snapshot.MaxHealth <= 0)
            {
                counts.HiddenZeroHealth++;
                AddSampleDiagnostic(sampleDiagnostics, enemy, sample, snapshot, "hiddenZeroHealth");
                presenter.Hide(sample.EnemyId);
            }
            else if (!config.ShowFullHealthEnemies && snapshot.IsFullHealth)
            {
                counts.HiddenFull++;
                AddSampleDiagnostic(sampleDiagnostics, enemy, sample, snapshot, "hiddenFullHealth");
                presenter.Hide(sample.EnemyId);
            }
            else
            {
                var worldPosition = presenter.GetWorldPosition(enemy);
                var squaredDistanceToCamera = (cameraPosition - worldPosition).sqrMagnitude;
                if (!visibilityRules.ShouldShowBySquaredDistance(snapshot, squaredDistanceToCamera))
                {
                    counts.HiddenDistance++;
                    AddSampleDiagnostic(sampleDiagnostics, enemy, sample, snapshot, "hiddenDistance");
                    presenter.Hide(sample.EnemyId);
                }
                else
                {
                    counts.Shown++;
                    AddSampleDiagnostic(sampleDiagnostics, enemy, sample, snapshot, "shown");
                    presenter.ShowOrUpdate(enemy, snapshot, worldPosition, camera, billboardRotation);
                }
            }
        }

        var retainedEnemyStateIdsForScan = retainedEnemyStateIds.Update(activeEnemyIds);
        var retainedNetworkStateIdsForScan = retainedNetworkStateIds.Update(activeNetworkObjectIds);
        tracker.KeepOnly(retainedEnemyStateIdsForScan);
        EnemyHealthOverrideStore.KeepOnly(retainedEnemyStateIdsForScan, retainedNetworkStateIdsForScan);
        networkSync.KeepOnlyClientSnapshots(retainedNetworkStateIdsForScan);
        networkSync.KeepOnlyHostSnapshots(retainedNetworkStateIdsForScan);
        networkSync.FlushHostSnapshots();
        presenter.HideMissing(activeEnemyIds);
        counts.Active = presenter.ActiveCount;
        counts.RetainedEnemyStates = retainedEnemyStateIdsForScan.Count - activeEnemyIds.Count;
        counts.RetainedNetworkStates = retainedNetworkStateIdsForScan.Count - activeNetworkObjectIds.Count;
        LogDiagnosticsIfDue(counts, camera, "scan", sampleDiagnostics);
    }

    private void LogDiagnosticsIfDue(DiagnosticScanCounts counts, Camera? camera, string source, List<string>? sampleDiagnostics = null)
    {
        if (config == null || logger == null || !config.DiagnosticsEnabled || Time.unscaledTime < nextDiagnosticLogTime)
        {
            return;
        }

        nextDiagnosticLogTime = Time.unscaledTime + config.DiagnosticsLogInterval;
        var cameraText = camera != null ? $"{camera.name}@{FormatVector(camera.transform.position)} mask={camera.cullingMask}" : "none";
        var unresolvedNames = maxHealthResolver.FormatUnresolvedNamesForDiagnostics(5);
        var sampleDiagnosticsText = sampleDiagnostics != null && sampleDiagnostics.Count > 0
            ? $", samples=[{string.Join("; ", sampleDiagnostics)}]"
            : string.Empty;
        logger.LogInfo(
            $"Diagnostics {source}: camera={cameraText}, spawned={counts.Spawned}, readable={counts.Readable}, shown={counts.Shown}, active={counts.Active}, " +
            $"rejectedNull={counts.RejectedMissingEnemy}, rejectedNoType={counts.RejectedMissingEnemyType}, rejectedCannotDie={counts.RejectedCannotDie}, " +
            $"hostSync={networkSync.HasHostSync}, usedHostSync={counts.UsedHostSync}, hiddenWaitingHostSync={counts.HiddenWaitingForHostSync}, " +
            $"hiddenDead={counts.HiddenDead}, hiddenSettling={counts.HiddenSettling}, hiddenSpawnSettling={counts.HiddenSpawnSettling}, hiddenZeroHp={counts.HiddenZeroHealth}, hiddenFull={counts.HiddenFull}, hiddenDistance={counts.HiddenDistance}, " +
            $"retainedEnemyStates={counts.RetainedEnemyStates}, retainedNetworkStates={counts.RetainedNetworkStates}, unresolvedMaxHealth={maxHealthResolver.UnresolvedCount}, unresolvedNames={unresolvedNames}{sampleDiagnosticsText}");
    }

    private void ApplyHostSyncedHealth(EnemyAI enemy, EnemyHealthSample sample, EnemyHealthSnapshot snapshot)
    {
        if (snapshot.IsDead || snapshot.CurrentHealth <= 0 || enemy.isEnemyDead)
        {
            return;
        }

        if (enemy.enemyHP != snapshot.CurrentHealth)
        {
            enemy.enemyHP = snapshot.CurrentHealth;
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

    private void LogDebugHeartbeatIfDue(Camera? camera)
    {
        if (config == null || logger == null || presenter == null || !config.DiagnosticsEnabled || Time.unscaledTime < nextDebugHeartbeatLogTime)
        {
            return;
        }

        nextDebugHeartbeatLogTime = Time.unscaledTime + config.DiagnosticsLogInterval;
        var cameraName = camera != null ? camera.name : "none";
        logger.LogInfo($"Debug heartbeat: enabled={config.Enabled}, camera='{cameraName}', active={presenter.ActiveCount}, testBar={presenter.DebugTestBarVisible}");
    }

    private static void AddReadFailure(ref DiagnosticScanCounts counts, EnemyHealthReadFailure failure)
    {
        switch (failure)
        {
            case EnemyHealthReadFailure.MissingEnemy:
                counts.RejectedMissingEnemy++;
                break;
            case EnemyHealthReadFailure.MissingEnemyType:
                counts.RejectedMissingEnemyType++;
                break;
            case EnemyHealthReadFailure.CannotDie:
                counts.RejectedCannotDie++;
                break;
        }
    }

    private bool ShouldCollectSampleDiagnostics()
    {
        return config != null &&
            config.DiagnosticsLogEnemyHealthSamples &&
            Time.unscaledTime >= nextDiagnosticLogTime;
    }

    private static void AddMissingEnemyDiagnostic(List<string>? sampleDiagnostics, int index)
    {
        if (!CanAddSampleDiagnostic(sampleDiagnostics))
        {
            return;
        }

        sampleDiagnostics!.Add($"readFailure:index={index}, reason=MissingEnemy");
    }

    private static void AddReadFailureDiagnostic(List<string>? sampleDiagnostics, EnemyAI enemy, EnemyHealthReadFailure failure)
    {
        if (!CanAddSampleDiagnostic(sampleDiagnostics))
        {
            return;
        }

        var displayName = enemy.enemyType != null && !string.IsNullOrWhiteSpace(enemy.enemyType.enemyName)
            ? enemy.enemyType.enemyName
            : enemy.name;
        var canDie = enemy.enemyType != null ? enemy.enemyType.canDie.ToString() : "unknown";
        var networkObjectId = enemy.NetworkObject != null ? enemy.NetworkObject.NetworkObjectId.ToString() : "none";
        sampleDiagnostics!.Add(
            $"readFailure:name='{displayName}', id={enemy.GetInstanceID()}, netId={networkObjectId}, hp={enemy.enemyHP}, " +
            $"dead={enemy.isEnemyDead}, canDie={canDie}, reason={failure}");
    }

    private static void AddSampleDiagnostic(
        List<string>? sampleDiagnostics,
        EnemyAI enemy,
        EnemyHealthSample sample,
        EnemyHealthSnapshot snapshot,
        string result)
    {
        if (!CanAddSampleDiagnostic(sampleDiagnostics))
        {
            return;
        }

        var canDie = enemy.enemyType != null ? enemy.enemyType.canDie.ToString() : "unknown";
        var networkObjectId = sample.NetworkObjectId != 0UL ? sample.NetworkObjectId.ToString() : "none";
        sampleDiagnostics!.Add(
            $"sample:name='{sample.DisplayName}', id={sample.EnemyId}, netId={networkObjectId}, current={sample.CurrentHealth}, " +
            $"resolvedMax={sample.MaxHealth}, trackedMax={snapshot.MaxHealth}, fraction={snapshot.HealthFraction:0.###}, " +
            $"full={snapshot.IsFullHealth}, settling={snapshot.IsMaxHealthSettling}, spawnSettling={snapshot.IsSpawnHealthSettling}, " +
            $"clientDesync={snapshot.IsClientHealthDesynced}, dead={snapshot.IsDead}, canDie={canDie}, result={result}");
    }

    private static bool CanAddSampleDiagnostic(List<string>? sampleDiagnostics)
    {
        return sampleDiagnostics != null && sampleDiagnostics.Count < MaxSampleDiagnosticsPerScan;
    }

    private static string FormatVector(Vector3 value)
    {
        return $"({value.x:0.0},{value.y:0.0},{value.z:0.0})";
    }

    private struct DiagnosticScanCounts
    {
        public static readonly DiagnosticScanCounts Empty = new();

        public int Spawned;
        public int Readable;
        public int Shown;
        public int Active;
        public int RejectedMissingEnemy;
        public int RejectedMissingEnemyType;
        public int RejectedCannotDie;
        public int UsedHostSync;
        public int HiddenWaitingForHostSync;
        public int HiddenDead;
        public int HiddenSettling;
        public int HiddenSpawnSettling;
        public int HiddenZeroHealth;
        public int HiddenFull;
        public int HiddenDistance;
        public int RetainedEnemyStates;
        public int RetainedNetworkStates;
    }
}
