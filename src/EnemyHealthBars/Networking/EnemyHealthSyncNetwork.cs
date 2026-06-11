using System.Collections.Generic;
using Auuueser.EnemyHealthBars.Core.Domain;
using Unity.Collections;
using Unity.Netcode;

namespace Auuueser.EnemyHealthBars.Networking;

internal sealed class EnemyHealthSyncNetwork
{
    private const byte ProtocolVersion = 2;
    private const byte DeadFlag = 1;
    private const byte MaxHealthSettlingFlag = 2;
    private const byte SpawnHealthSettlingFlag = 4;
    private const int MaxPayloadBytes = 1200;
    private const int MaxSnapshotsPerMessage = 64;
    private const float ClientHelloInterval = 1f;
    private const float ClientFallbackDelay = 3f;
    private const string HelloMessage = "auuueser.enemyhealthbar.hello.v1";
    private const string HealthMessage = "auuueser.enemyhealthbar.health.v1";

    private readonly EnemyHealthSyncStore clientSnapshots = new();
    private readonly Dictionary<ulong, EnemyHealthSyncSnapshot> lastSentSnapshots = new();
    private readonly Dictionary<ulong, int> lastSentEnemyIds = new();
    private readonly HashSet<ulong> syncClientIds = new();
    private readonly List<ulong> sendTargetClientIds = new();
    private readonly List<ulong> removedClientIds = new();
    private readonly List<EnemyHealthSyncSnapshot> pendingSnapshots = new();

    private FastBufferWriter writer;
    private NetworkManager? registeredNetworkManager;
    private bool handlersRegistered;
    private bool hostSyncActive;
    private bool forceFullRefresh;
    private bool runtimeStateCleared = true;
    private uint sequence;
    private float nextHelloTime;
    private float clientSyncStartTime = -1f;

    public bool HasHostSync => hostSyncActive;

    public void Tick(bool enabled, float currentTime)
    {
        var networkManager = NetworkManager.Singleton;
        if (!enabled || !IsNetworkUsable(networkManager))
        {
            ResetRuntimeStateIfNeeded();
            return;
        }

        runtimeStateCleared = false;
        EnsureHandlers(networkManager!);
        SendClientHelloIfNeeded(networkManager!, currentTime);
    }

    public bool TryGetHostSnapshot(EnemyHealthSample sample, out EnemyHealthSnapshot snapshot)
    {
        snapshot = default;
        if (sample.NetworkObjectId == 0UL || !clientSnapshots.TryGetProjected(sample, out var syncSnapshot))
        {
            return false;
        }

        snapshot = syncSnapshot.ToEnemyHealthSnapshot(sample.EnemyId, sample.DisplayName);
        return true;
    }

    public void ConfirmHostSnapshotApplied(EnemyHealthSample sample, EnemyHealthSnapshot snapshot)
    {
        if (sample.NetworkObjectId == 0UL || snapshot.IsDead || snapshot.CurrentHealth <= 0)
        {
            return;
        }

        clientSnapshots.ConfirmProjectedHealthApplied(sample.NetworkObjectId, snapshot.CurrentHealth);
    }

    public bool ShouldSuppressLocalSnapshot(EnemyHealthSample sample, float currentTime)
    {
        if (sample.NetworkObjectId == 0UL)
        {
            return false;
        }

        var networkManager = NetworkManager.Singleton;
        if (!IsClientOnly(networkManager))
        {
            return false;
        }

        if (hostSyncActive)
        {
            return true;
        }

        return clientSyncStartTime >= 0f && currentTime - clientSyncStartTime < ClientFallbackDelay;
    }

    public void KeepOnlyClientSnapshots(ISet<ulong> activeNetworkObjectIds)
    {
        clientSnapshots.KeepOnly(activeNetworkObjectIds);
    }

    public void KeepOnlyHostSnapshots(ISet<ulong> activeNetworkObjectIds)
    {
        removedClientIds.Clear();

        foreach (var networkObjectId in lastSentSnapshots.Keys)
        {
            if (!activeNetworkObjectIds.Contains(networkObjectId))
            {
                removedClientIds.Add(networkObjectId);
            }
        }

        foreach (var networkObjectId in removedClientIds)
        {
            lastSentSnapshots.Remove(networkObjectId);
            lastSentEnemyIds.Remove(networkObjectId);
        }
    }

    public void QueueHostSnapshot(EnemyHealthSample sample, EnemyHealthSnapshot snapshot)
    {
        var networkManager = NetworkManager.Singleton;
        if (!IsHostPublisherReady(networkManager) ||
            sample.NetworkObjectId == 0UL ||
            syncClientIds.Count == 0)
        {
            return;
        }

        var syncSnapshot = new EnemyHealthSyncSnapshot(
            sample.NetworkObjectId,
            snapshot.CurrentHealth,
            snapshot.MaxHealth,
            snapshot.IsDead,
            0U,
            snapshot.IsMaxHealthSettling,
            snapshot.IsSpawnHealthSettling,
            sample.EnemyId);

        if (!forceFullRefresh &&
            lastSentSnapshots.TryGetValue(sample.NetworkObjectId, out var sentSnapshot) &&
            lastSentEnemyIds.TryGetValue(sample.NetworkObjectId, out var sentEnemyId) &&
            sentEnemyId == sample.EnemyId &&
            sentSnapshot.CurrentHealth == syncSnapshot.CurrentHealth &&
            sentSnapshot.MaxHealth == syncSnapshot.MaxHealth &&
            sentSnapshot.IsDead == syncSnapshot.IsDead &&
            sentSnapshot.IsMaxHealthSettling == syncSnapshot.IsMaxHealthSettling &&
            sentSnapshot.IsSpawnHealthSettling == syncSnapshot.IsSpawnHealthSettling)
        {
            return;
        }

        pendingSnapshots.Add(syncSnapshot);
    }

    public void FlushHostSnapshots()
    {
        var networkManager = NetworkManager.Singleton;
        if (!IsHostPublisherReady(networkManager) || pendingSnapshots.Count == 0)
        {
            pendingSnapshots.Clear();
            return;
        }

        BuildSendTargets(networkManager!);
        if (sendTargetClientIds.Count == 0)
        {
            pendingSnapshots.Clear();
            return;
        }

        var index = 0;
        while (index < pendingSnapshots.Count)
        {
            index = SendSnapshotChunk(networkManager!, index);
        }

        pendingSnapshots.Clear();
        forceFullRefresh = false;
    }

    public void Dispose()
    {
        UnregisterHandlers();
        if (writer.IsInitialized)
        {
            writer.Dispose();
        }
    }

    private void ResetRuntimeState()
    {
        UnregisterHandlers();
        clientSnapshots.Clear();
        syncClientIds.Clear();
        sendTargetClientIds.Clear();
        removedClientIds.Clear();
        pendingSnapshots.Clear();
        lastSentSnapshots.Clear();
        lastSentEnemyIds.Clear();
        hostSyncActive = false;
        forceFullRefresh = false;
        clientSyncStartTime = -1f;
        runtimeStateCleared = true;
    }

    private void ResetRuntimeStateIfNeeded()
    {
        if (runtimeStateCleared)
        {
            return;
        }

        ResetRuntimeState();
    }

    private void EnsureHandlers(NetworkManager networkManager)
    {
        if (handlersRegistered && registeredNetworkManager == networkManager)
        {
            return;
        }

        UnregisterHandlers();
        networkManager.CustomMessagingManager.RegisterNamedMessageHandler(HelloMessage, HandleHelloMessage);
        networkManager.CustomMessagingManager.RegisterNamedMessageHandler(HealthMessage, HandleHealthMessage);
        registeredNetworkManager = networkManager;
        handlersRegistered = true;
    }

    private void UnregisterHandlers()
    {
        if (!handlersRegistered || registeredNetworkManager == null || registeredNetworkManager.CustomMessagingManager == null)
        {
            handlersRegistered = false;
            registeredNetworkManager = null;
            return;
        }

        registeredNetworkManager.CustomMessagingManager.UnregisterNamedMessageHandler(HelloMessage);
        registeredNetworkManager.CustomMessagingManager.UnregisterNamedMessageHandler(HealthMessage);
        handlersRegistered = false;
        registeredNetworkManager = null;
    }

    private void SendClientHelloIfNeeded(NetworkManager networkManager, float currentTime)
    {
        if (!IsClientOnly(networkManager))
        {
            clientSyncStartTime = -1f;
            return;
        }

        if (clientSyncStartTime < 0f)
        {
            clientSyncStartTime = currentTime;
        }

        if (hostSyncActive || currentTime < nextHelloTime)
        {
            return;
        }

        nextHelloTime = currentTime + ClientHelloInterval;
        EnsureWriter();
        writer.Seek(0);
        writer.Truncate(0);
        writer.WriteByteSafe(ProtocolVersion);
        networkManager.CustomMessagingManager.SendNamedMessage(
            HelloMessage,
            NetworkManager.ServerClientId,
            writer,
            NetworkDelivery.ReliableSequenced);
    }

    private void HandleHelloMessage(ulong senderClientId, FastBufferReader payload)
    {
        var networkManager = NetworkManager.Singleton;
        if (!IsHostPublisherReady(networkManager) || senderClientId == networkManager!.LocalClientId)
        {
            return;
        }

        payload.ReadByteSafe(out var protocolVersion);
        if (protocolVersion != ProtocolVersion)
        {
            return;
        }

        if (syncClientIds.Add(senderClientId))
        {
            forceFullRefresh = true;
        }
    }

    private void HandleHealthMessage(ulong senderClientId, FastBufferReader payload)
    {
        var networkManager = NetworkManager.Singleton;
        if (!IsClientOnly(networkManager) || senderClientId != NetworkManager.ServerClientId)
        {
            return;
        }

        payload.ReadByteSafe(out var protocolVersion);
        if (protocolVersion != ProtocolVersion)
        {
            return;
        }

        payload.ReadValueSafe(out uint incomingSequence);
        payload.ReadValueSafe(out ushort snapshotCount);
        hostSyncActive = true;

        for (var i = 0; i < snapshotCount; i++)
        {
            payload.ReadValueSafe(out ulong networkObjectId);
            payload.ReadValueSafe(out int currentHealth);
            payload.ReadValueSafe(out int maxHealth);
            payload.ReadByteSafe(out var flags);

            clientSnapshots.ApplyHostSnapshot(new EnemyHealthSyncSnapshot(
                networkObjectId,
                currentHealth,
                maxHealth,
                (flags & DeadFlag) != 0,
                incomingSequence,
                (flags & MaxHealthSettlingFlag) != 0,
                (flags & SpawnHealthSettlingFlag) != 0));
        }
    }

    private int SendSnapshotChunk(NetworkManager networkManager, int startIndex)
    {
        EnsureWriter();
        writer.Seek(0);
        writer.Truncate(0);

        sequence++;
        if (sequence == 0U)
        {
            sequence = 1U;
        }

        var count = pendingSnapshots.Count - startIndex;
        if (count > MaxSnapshotsPerMessage)
        {
            count = MaxSnapshotsPerMessage;
        }

        writer.WriteByteSafe(ProtocolVersion);
        writer.WriteValueSafe(sequence);
        writer.WriteValueSafe((ushort)count);

        var endIndex = startIndex + count;
        for (var i = startIndex; i < endIndex; i++)
        {
            var snapshot = pendingSnapshots[i] with
            {
                Sequence = sequence,
            };
            var flags = ComposeFlags(snapshot);

            writer.WriteValueSafe(snapshot.NetworkObjectId);
            writer.WriteValueSafe(snapshot.CurrentHealth);
            writer.WriteValueSafe(snapshot.MaxHealth);
            writer.WriteByteSafe(flags);
            lastSentSnapshots[snapshot.NetworkObjectId] = snapshot;
            lastSentEnemyIds[snapshot.NetworkObjectId] = snapshot.EnemyId;
        }

        networkManager.CustomMessagingManager.SendNamedMessage(
            HealthMessage,
            sendTargetClientIds,
            writer,
            NetworkDelivery.ReliableSequenced);

        return endIndex;
    }

    private static byte ComposeFlags(EnemyHealthSyncSnapshot snapshot)
    {
        var flags = snapshot.IsDead ? DeadFlag : (byte)0;
        if (snapshot.IsMaxHealthSettling)
        {
            flags |= MaxHealthSettlingFlag;
        }

        if (snapshot.IsSpawnHealthSettling)
        {
            flags |= SpawnHealthSettlingFlag;
        }

        return flags;
    }

    private void BuildSendTargets(NetworkManager networkManager)
    {
        sendTargetClientIds.Clear();
        removedClientIds.Clear();

        foreach (var clientId in syncClientIds)
        {
            if (clientId == networkManager.LocalClientId || !IsConnectedClient(networkManager, clientId))
            {
                removedClientIds.Add(clientId);
            }
            else
            {
                sendTargetClientIds.Add(clientId);
            }
        }

        foreach (var clientId in removedClientIds)
        {
            syncClientIds.Remove(clientId);
        }
    }

    private void EnsureWriter()
    {
        if (!writer.IsInitialized)
        {
            writer = new FastBufferWriter(MaxPayloadBytes, Allocator.Persistent, MaxPayloadBytes);
        }
    }

    private static bool IsConnectedClient(NetworkManager networkManager, ulong clientId)
    {
        foreach (var connectedClientId in networkManager.ConnectedClientsIds)
        {
            if (connectedClientId == clientId)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsHostPublisherReady(NetworkManager? networkManager)
    {
        return IsNetworkUsable(networkManager) && networkManager!.IsServer;
    }

    private static bool IsClientOnly(NetworkManager? networkManager)
    {
        return IsNetworkUsable(networkManager) && networkManager!.IsClient && !networkManager.IsServer;
    }

    private static bool IsNetworkUsable(NetworkManager? networkManager)
    {
        return networkManager != null && networkManager.IsListening && networkManager.CustomMessagingManager != null;
    }
}
