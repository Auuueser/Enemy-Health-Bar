using System.Collections.Generic;
using Auuueser.EnemyHealthBars.Configuration;
using Auuueser.EnemyHealthBars.Core.Domain;
using Auuueser.EnemyHealthBars.Game;
using Auuueser.EnemyHealthBars.Networking;
using BepInEx.Logging;
using UnityEngine;

namespace Auuueser.EnemyHealthBars.Runtime;

internal struct EnemyScanCounts
{
    public static readonly EnemyScanCounts Empty = new();

    public int Spawned;
    public int Unlisted;
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
    public int HiddenZeroHealth;
    public int HiddenFull;
    public int HiddenDistance;
    public int RetainedEnemyStates;
    public int RetainedNetworkStates;

    public bool SameAs(in EnemyScanCounts other)
    {
        return Spawned == other.Spawned &&
            Unlisted == other.Unlisted &&
            Readable == other.Readable &&
            Shown == other.Shown &&
            Active == other.Active &&
            RejectedMissingEnemy == other.RejectedMissingEnemy &&
            RejectedMissingEnemyType == other.RejectedMissingEnemyType &&
            RejectedCannotDie == other.RejectedCannotDie &&
            UsedHostSync == other.UsedHostSync &&
            HiddenWaitingForHostSync == other.HiddenWaitingForHostSync &&
            HiddenDead == other.HiddenDead &&
            HiddenSettling == other.HiddenSettling &&
            HiddenZeroHealth == other.HiddenZeroHealth &&
            HiddenFull == other.HiddenFull &&
            HiddenDistance == other.HiddenDistance &&
            RetainedEnemyStates == other.RetainedEnemyStates &&
            RetainedNetworkStates == other.RetainedNetworkStates;
    }

    public void CountReadFailure(EnemyHealthReadFailure failure)
    {
        switch (failure)
        {
            case EnemyHealthReadFailure.MissingEnemy:
                RejectedMissingEnemy++;
                break;
            case EnemyHealthReadFailure.MissingEnemyType:
                RejectedMissingEnemyType++;
                break;
            case EnemyHealthReadFailure.CannotDie:
                RejectedCannotDie++;
                break;
        }
    }

    public void CountHidden(HealthBarVisibility visibility)
    {
        switch (visibility)
        {
            case HealthBarVisibility.HiddenDead:
                HiddenDead++;
                break;
            case HealthBarVisibility.HiddenMaxHealthSettling:
                HiddenSettling++;
                break;
            case HealthBarVisibility.HiddenZeroHealth:
                HiddenZeroHealth++;
                break;
            case HealthBarVisibility.HiddenFullHealth:
                HiddenFull++;
                break;
        }
    }
}

// Idle states (disabled, no camera, no round) log once when entered; unchanged scan summaries and
// heartbeats repeat only at a slow interval, so menus and orbit do not flood LogOutput.log.
internal sealed class EnemyScanDiagnostics
{
    public const string ScanSource = "scan";

    private const int MaxSampleDiagnosticsPerScan = 8;
    private const float UnchangedScanRepeatInterval = 30f;
    private const float UnchangedHeartbeatRepeatInterval = 60f;

    private readonly ModConfig config;
    private readonly ManualLogSource logger;
    private readonly EnemyMaxHealthResolver maxHealthResolver;
    private readonly EnemyHealthSyncNetwork networkSync;
    private readonly List<string> sampleDiagnostics = new(MaxSampleDiagnosticsPerScan);
    private bool collectSamples;
    private float nextDiagnosticLogTime;
    private float nextDebugHeartbeatLogTime;
    private string? lastLoggedSource;
    private EnemyScanCounts lastLoggedCounts;
    private float lastScanLogTime = float.NegativeInfinity;
    private string? lastHeartbeat;
    private float lastHeartbeatLogTime = float.NegativeInfinity;

    public EnemyScanDiagnostics(
        ModConfig config,
        ManualLogSource logger,
        EnemyMaxHealthResolver maxHealthResolver,
        EnemyHealthSyncNetwork networkSync)
    {
        this.config = config;
        this.logger = logger;
        this.maxHealthResolver = maxHealthResolver;
        this.networkSync = networkSync;
    }

    public void BeginScan(float currentTime)
    {
        sampleDiagnostics.Clear();
        collectSamples = config.DiagnosticsLogEnemyHealthSamples && currentTime >= nextDiagnosticLogTime;
    }

    public void AddMissingEnemy(int index)
    {
        if (!CanAddSample())
        {
            return;
        }

        sampleDiagnostics.Add($"readFailure:index={index}, reason=MissingEnemy");
    }

    public void AddReadFailure(EnemyAI enemy, EnemyHealthReadFailure failure)
    {
        if (!CanAddSample())
        {
            return;
        }

        var displayName = enemy.enemyType != null && !string.IsNullOrWhiteSpace(enemy.enemyType.enemyName)
            ? enemy.enemyType.enemyName
            : enemy.name;
        var canDie = enemy.enemyType != null ? enemy.enemyType.canDie.ToString() : "unknown";
        var networkObjectId = enemy.NetworkObject != null ? enemy.NetworkObject.NetworkObjectId.ToString() : "none";
        sampleDiagnostics.Add(
            $"readFailure:name='{displayName}', id={enemy.GetInstanceID()}, netId={networkObjectId}, hp={enemy.enemyHP}, " +
            $"dead={enemy.isEnemyDead}, canDie={canDie}, reason={failure}");
    }

    public void AddSample(EnemyAI enemy, EnemyHealthSample sample, EnemyHealthSnapshot snapshot, string result)
    {
        if (!CanAddSample())
        {
            return;
        }

        var canDie = enemy.enemyType != null ? enemy.enemyType.canDie.ToString() : "unknown";
        var networkObjectId = sample.NetworkObjectId != 0UL ? sample.NetworkObjectId.ToString() : "none";
        sampleDiagnostics.Add(
            $"sample:name='{sample.DisplayName}', id={sample.EnemyId}, netId={networkObjectId}, current={sample.CurrentHealth}, " +
            $"resolvedMax={sample.MaxHealth}, trackedMax={snapshot.MaxHealth}, preHit={sample.HealthBeforeFirstHit}, fraction={snapshot.HealthFraction:0.###}, " +
            $"full={snapshot.IsFullHealth}, settling={snapshot.IsMaxHealthSettling}, spawnSettling={snapshot.IsSpawnHealthSettling}, " +
            $"clientDesync={snapshot.IsClientHealthDesynced}, dead={snapshot.IsDead}, canDie={canDie}, result={result}");
    }

    public void LogScanIfDue(in EnemyScanCounts counts, Camera? camera, string source, float currentTime)
    {
        if (!config.DiagnosticsEnabled)
        {
            lastLoggedSource = null;
            return;
        }

        if (source != ScanSource)
        {
            if (source == lastLoggedSource)
            {
                return;
            }
        }
        else
        {
            if (currentTime < nextDiagnosticLogTime)
            {
                return;
            }

            nextDiagnosticLogTime = currentTime + config.DiagnosticsLogInterval;
            var unchanged = lastLoggedSource == ScanSource && sampleDiagnostics.Count == 0 && counts.SameAs(lastLoggedCounts);
            if (unchanged && currentTime - lastScanLogTime < UnchangedScanRepeatInterval)
            {
                return;
            }
        }

        lastLoggedSource = source;
        lastLoggedCounts = counts;
        lastScanLogTime = currentTime;
        var cameraText = camera != null ? $"{camera.name}@{FormatVector(camera.transform.position)} mask={camera.cullingMask}" : "none";
        var unresolvedNames = maxHealthResolver.FormatUnresolvedNamesForDiagnostics(5);
        var sampleDiagnosticsText = sampleDiagnostics.Count > 0
            ? $", samples=[{string.Join("; ", sampleDiagnostics)}]"
            : string.Empty;
        logger.LogInfo(
            $"Diagnostics {source}: camera={cameraText}, spawned={counts.Spawned}, unlisted={counts.Unlisted}, readable={counts.Readable}, shown={counts.Shown}, active={counts.Active}, " +
            $"rejectedNull={counts.RejectedMissingEnemy}, rejectedNoType={counts.RejectedMissingEnemyType}, rejectedCannotDie={counts.RejectedCannotDie}, " +
            $"hostSync={networkSync.HasHostSync}, usedHostSync={counts.UsedHostSync}, hiddenWaitingHostSync={counts.HiddenWaitingForHostSync}, " +
            $"hiddenDead={counts.HiddenDead}, hiddenSettling={counts.HiddenSettling}, hiddenZeroHp={counts.HiddenZeroHealth}, hiddenFull={counts.HiddenFull}, hiddenDistance={counts.HiddenDistance}, " +
            $"retainedEnemyStates={counts.RetainedEnemyStates}, retainedNetworkStates={counts.RetainedNetworkStates}, hitPatch={EnemyHitPatches.IsInstalled}, hitLedger={EnemyHitPatches.Ledger.Count}, " +
            $"unresolvedMaxHealth={maxHealthResolver.UnresolvedCount}, unresolvedNames={unresolvedNames}{sampleDiagnosticsText}");
    }

    public void LogHeartbeatIfDue(Camera? camera, int activeBars, bool testBarVisible, float currentTime)
    {
        if (!config.DiagnosticsEnabled || currentTime < nextDebugHeartbeatLogTime)
        {
            return;
        }

        nextDebugHeartbeatLogTime = currentTime + config.DiagnosticsLogInterval;
        var cameraName = camera != null ? camera.name : "none";
        var heartbeat = $"Debug heartbeat: enabled={config.Enabled}, camera='{cameraName}', active={activeBars}, testBar={testBarVisible}";
        if (heartbeat == lastHeartbeat && currentTime - lastHeartbeatLogTime < UnchangedHeartbeatRepeatInterval)
        {
            return;
        }

        lastHeartbeat = heartbeat;
        lastHeartbeatLogTime = currentTime;
        logger.LogInfo(heartbeat);
    }

    public static string DescribeHidden(HealthBarVisibility visibility)
    {
        return visibility switch
        {
            HealthBarVisibility.HiddenDead => "hiddenDead",
            HealthBarVisibility.HiddenMaxHealthSettling => "hiddenSettling",
            HealthBarVisibility.HiddenZeroHealth => "hiddenZeroHealth",
            HealthBarVisibility.HiddenFullHealth => "hiddenFullHealth",
            _ => "shown",
        };
    }

    private bool CanAddSample()
    {
        return collectSamples && sampleDiagnostics.Count < MaxSampleDiagnosticsPerScan;
    }

    private static string FormatVector(Vector3 value)
    {
        return $"({value.x:0.0},{value.y:0.0},{value.z:0.0})";
    }
}
