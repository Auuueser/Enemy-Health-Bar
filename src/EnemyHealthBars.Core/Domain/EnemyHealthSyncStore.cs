using System.Collections.Generic;

namespace Auuueser.EnemyHealthBars.Core.Domain;

public sealed class EnemyHealthSyncStore
{
    private readonly Dictionary<ulong, EnemyHealthSyncSnapshot> snapshots = new();
    private readonly Dictionary<ulong, ClientProjectionState> projectionStates = new();
    private readonly Dictionary<ulong, int> localEnemyIds = new();
    private readonly List<ulong> removedNetworkObjectIds = new();

    public int Count => snapshots.Count;

    public bool ApplyHostSnapshot(EnemyHealthSyncSnapshot snapshot)
    {
        if (snapshot.NetworkObjectId == 0UL)
        {
            return false;
        }

        var normalized = Normalize(snapshot);
        if (snapshots.TryGetValue(normalized.NetworkObjectId, out var existing))
        {
            if (normalized.Sequence <= existing.Sequence)
            {
                return false;
            }

            snapshots[normalized.NetworkObjectId] = normalized;
            return HasChanged(existing, normalized);
        }

        snapshots.Add(normalized.NetworkObjectId, normalized);
        return true;
    }

    public bool TryGet(ulong networkObjectId, out EnemyHealthSyncSnapshot snapshot)
    {
        return snapshots.TryGetValue(networkObjectId, out snapshot);
    }

    public bool TryGetProjected(EnemyHealthSample sample, out EnemyHealthSyncSnapshot snapshot)
    {
        snapshot = default;
        if (sample.NetworkObjectId == 0UL || !snapshots.TryGetValue(sample.NetworkObjectId, out var hostSnapshot))
        {
            return false;
        }

        if (localEnemyIds.TryGetValue(sample.NetworkObjectId, out var localEnemyId) && localEnemyId != sample.EnemyId)
        {
            Remove(sample.NetworkObjectId);
            return false;
        }

        localEnemyIds[sample.NetworkObjectId] = sample.EnemyId;
        snapshot = Project(sample, hostSnapshot);
        return true;
    }

    public void ConfirmProjectedHealthApplied(ulong networkObjectId, int currentHealth)
    {
        if (networkObjectId == 0UL || !projectionStates.TryGetValue(networkObjectId, out var projection))
        {
            return;
        }

        projectionStates[networkObjectId] = projection.WithAppliedCurrentHealth(currentHealth);
    }

    public void Remove(ulong networkObjectId)
    {
        snapshots.Remove(networkObjectId);
        projectionStates.Remove(networkObjectId);
        localEnemyIds.Remove(networkObjectId);
    }

    public void Clear()
    {
        snapshots.Clear();
        projectionStates.Clear();
        localEnemyIds.Clear();
    }

    public void KeepOnly(ISet<ulong> activeNetworkObjectIds)
    {
        removedNetworkObjectIds.Clear();

        foreach (var networkObjectId in snapshots.Keys)
        {
            if (!activeNetworkObjectIds.Contains(networkObjectId))
            {
                removedNetworkObjectIds.Add(networkObjectId);
            }
        }

        foreach (var networkObjectId in removedNetworkObjectIds)
        {
            snapshots.Remove(networkObjectId);
            projectionStates.Remove(networkObjectId);
            localEnemyIds.Remove(networkObjectId);
        }
    }

    private EnemyHealthSyncSnapshot Project(EnemyHealthSample sample, EnemyHealthSyncSnapshot hostSnapshot)
    {
        var localCurrentHealth = sample.CurrentHealth < 0 ? 0 : sample.CurrentHealth;
        var hasProjection = projectionStates.TryGetValue(sample.NetworkObjectId, out var projection);
        if (!hasProjection || !projection.Matches(hostSnapshot))
        {
            projection = CreateProjection(localCurrentHealth, hostSnapshot, hasProjection, projection);
        }

        var projectedCurrentHealth = projection.ProjectedBaselineHealth;
        var localDamage = projection.LocalBaselineHealth - localCurrentHealth;
        if (localDamage > 0 && projectedCurrentHealth > 0)
        {
            projectedCurrentHealth -= localDamage;
        }

        projectedCurrentHealth = ClampProjectedCurrentHealth(projectedCurrentHealth, hostSnapshot.MaxHealth, hostSnapshot.IsDead || sample.IsDead);
        projectionStates[sample.NetworkObjectId] = projection.WithLastProjectedCurrent(projectedCurrentHealth);

        return hostSnapshot with
        {
            CurrentHealth = projectedCurrentHealth,
            IsDead = hostSnapshot.IsDead || sample.IsDead,
        };
    }

    private ClientProjectionState CreateProjection(
        int localCurrentHealth,
        EnemyHealthSyncSnapshot hostSnapshot,
        bool hasPreviousProjection,
        ClientProjectionState previousProjection)
    {
        var projectedBaselineHealth = hostSnapshot.CurrentHealth;
        if (hasPreviousProjection &&
            hostSnapshot.MaxHealth == previousProjection.SourceHostMaxHealth &&
            localCurrentHealth < previousProjection.LocalBaselineHealth &&
            projectedBaselineHealth > previousProjection.LastProjectedCurrent)
        {
            projectedBaselineHealth = previousProjection.LastProjectedCurrent;
        }

        return new ClientProjectionState(
            hostSnapshot.Sequence,
            hostSnapshot.CurrentHealth,
            hostSnapshot.MaxHealth,
            hostSnapshot.IsDead,
            localCurrentHealth,
            projectedBaselineHealth,
            projectedBaselineHealth);
    }

    private static int ClampProjectedCurrentHealth(int currentHealth, int maxHealth, bool isDead)
    {
        if (currentHealth < 0)
        {
            currentHealth = 0;
        }

        if (maxHealth > 0 && currentHealth > maxHealth)
        {
            currentHealth = maxHealth;
        }

        return !isDead && maxHealth > 0 && currentHealth <= 0 ? 1 : currentHealth;
    }

    private static EnemyHealthSyncSnapshot Normalize(EnemyHealthSyncSnapshot snapshot)
    {
        var currentHealth = snapshot.CurrentHealth < 0 ? 0 : snapshot.CurrentHealth;
        var maxHealth = snapshot.MaxHealth < 0 ? 0 : snapshot.MaxHealth;
        return snapshot with
        {
            CurrentHealth = currentHealth,
            MaxHealth = maxHealth,
        };
    }

    private static bool HasChanged(EnemyHealthSyncSnapshot existing, EnemyHealthSyncSnapshot snapshot)
    {
        return existing.CurrentHealth != snapshot.CurrentHealth ||
            existing.MaxHealth != snapshot.MaxHealth ||
            existing.IsDead != snapshot.IsDead;
    }

    private readonly struct ClientProjectionState
    {
        public ClientProjectionState(
            uint sequence,
            int sourceHostCurrentHealth,
            int sourceHostMaxHealth,
            bool sourceIsDead,
            int localBaselineHealth,
            int projectedBaselineHealth,
            int lastProjectedCurrent)
        {
            Sequence = sequence;
            SourceHostCurrentHealth = sourceHostCurrentHealth;
            SourceHostMaxHealth = sourceHostMaxHealth;
            SourceIsDead = sourceIsDead;
            LocalBaselineHealth = localBaselineHealth;
            ProjectedBaselineHealth = projectedBaselineHealth;
            LastProjectedCurrent = lastProjectedCurrent;
        }

        public uint Sequence { get; }

        public int SourceHostCurrentHealth { get; }

        public int SourceHostMaxHealth { get; }

        public bool SourceIsDead { get; }

        public int LocalBaselineHealth { get; }

        public int ProjectedBaselineHealth { get; }

        public int LastProjectedCurrent { get; }

        public bool Matches(EnemyHealthSyncSnapshot snapshot)
        {
            return Sequence == snapshot.Sequence &&
                SourceHostCurrentHealth == snapshot.CurrentHealth &&
                SourceHostMaxHealth == snapshot.MaxHealth &&
                SourceIsDead == snapshot.IsDead;
        }

        public ClientProjectionState WithLastProjectedCurrent(int currentHealth)
        {
            return new ClientProjectionState(
                Sequence,
                SourceHostCurrentHealth,
                SourceHostMaxHealth,
                SourceIsDead,
                LocalBaselineHealth,
                ProjectedBaselineHealth,
                currentHealth);
        }

        public ClientProjectionState WithAppliedCurrentHealth(int currentHealth)
        {
            return new ClientProjectionState(
                Sequence,
                SourceHostCurrentHealth,
                SourceHostMaxHealth,
                SourceIsDead,
                currentHealth,
                currentHealth,
                currentHealth);
        }
    }
}
