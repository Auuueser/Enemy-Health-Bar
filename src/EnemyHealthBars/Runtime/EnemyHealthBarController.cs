using System.Collections.Generic;
using Auuueser.EnemyHealthBars.Configuration;
using Auuueser.EnemyHealthBars.Core.Domain;
using Auuueser.EnemyHealthBars.Game;
using Auuueser.EnemyHealthBars.Presentation;
using BepInEx.Logging;
using UnityEngine;

namespace Auuueser.EnemyHealthBars.Runtime;

internal sealed class EnemyHealthBarController : MonoBehaviour
{
    private readonly EnemyHealthTracker tracker = new();
    private readonly EnemyHealthReader healthReader = new();
    private readonly EnemyMaxHealthResolver maxHealthResolver = new();
    private readonly HashSet<int> activeEnemyIds = new();

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
        presenter?.Clear();
    }

    private void RefreshEnemies(Camera camera, Quaternion billboardRotation)
    {
        if (config == null || enemySource == null || presenter == null)
        {
            return;
        }

        activeEnemyIds.Clear();
        var visibilityRules = config.CreateVisibilityRules();
        var cameraPosition = camera.transform.position;

        var spawnedEnemies = enemySource.GetSpawnedEnemies();
        if (spawnedEnemies == null)
        {
            tracker.KeepOnly(activeEnemyIds);
            presenter.HideMissing(activeEnemyIds);
            LogDiagnosticsIfDue(DiagnosticScanCounts.Empty, camera, "SpawnedEnemies unavailable");
            return;
        }

        var counts = new DiagnosticScanCounts
        {
            Spawned = spawnedEnemies.Count,
        };

        for (var i = 0; i < spawnedEnemies.Count; i++)
        {
            var enemy = spawnedEnemies[i];
            if (enemy == null)
            {
                counts.RejectedMissingEnemy++;
                continue;
            }

            if (!healthReader.TryRead(enemy, config.ShowInvulnerableEnemies, maxHealthResolver, out var sample, out var readFailure))
            {
                AddReadFailure(ref counts, readFailure);
                continue;
            }

            counts.Readable++;
            activeEnemyIds.Add(sample.EnemyId);
            var snapshot = tracker.Track(sample, config.MaxHealthMode);

            if (snapshot.IsDead)
            {
                counts.HiddenDead++;
                presenter.Hide(sample.EnemyId);
            }
            else if (snapshot.CurrentHealth <= 0 || snapshot.MaxHealth <= 0)
            {
                counts.HiddenZeroHealth++;
                presenter.Hide(sample.EnemyId);
            }
            else if (!config.ShowFullHealthEnemies && snapshot.IsFullHealth)
            {
                counts.HiddenFull++;
                presenter.Hide(sample.EnemyId);
            }
            else
            {
                var worldPosition = presenter.GetWorldPosition(enemy);
                var squaredDistanceToCamera = (cameraPosition - worldPosition).sqrMagnitude;
                if (!visibilityRules.ShouldShowBySquaredDistance(snapshot, squaredDistanceToCamera))
                {
                    counts.HiddenDistance++;
                    presenter.Hide(sample.EnemyId);
                }
                else
                {
                    counts.Shown++;
                    presenter.ShowOrUpdate(enemy, snapshot, worldPosition, camera, billboardRotation);
                }
            }
        }

        tracker.KeepOnly(activeEnemyIds);
        presenter.HideMissing(activeEnemyIds);
        counts.Active = presenter.ActiveCount;
        LogDiagnosticsIfDue(counts, camera, "scan");
    }

    private void LogDiagnosticsIfDue(DiagnosticScanCounts counts, Camera? camera, string source)
    {
        if (config == null || logger == null || !config.DiagnosticsEnabled || Time.unscaledTime < nextDiagnosticLogTime)
        {
            return;
        }

        nextDiagnosticLogTime = Time.unscaledTime + config.DiagnosticsLogInterval;
        var cameraText = camera != null ? $"{camera.name}@{FormatVector(camera.transform.position)} mask={camera.cullingMask}" : "none";
        var unresolvedNames = maxHealthResolver.FormatUnresolvedNamesForDiagnostics(5);
        logger.LogInfo(
            $"Diagnostics {source}: camera={cameraText}, spawned={counts.Spawned}, readable={counts.Readable}, shown={counts.Shown}, active={counts.Active}, " +
            $"rejectedNull={counts.RejectedMissingEnemy}, rejectedNoType={counts.RejectedMissingEnemyType}, rejectedCannotDie={counts.RejectedCannotDie}, " +
            $"hiddenDead={counts.HiddenDead}, hiddenZeroHp={counts.HiddenZeroHealth}, hiddenFull={counts.HiddenFull}, hiddenDistance={counts.HiddenDistance}, " +
            $"unresolvedMaxHealth={maxHealthResolver.UnresolvedCount}, unresolvedNames={unresolvedNames}");
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
        public int HiddenDead;
        public int HiddenZeroHealth;
        public int HiddenFull;
        public int HiddenDistance;
    }
}
