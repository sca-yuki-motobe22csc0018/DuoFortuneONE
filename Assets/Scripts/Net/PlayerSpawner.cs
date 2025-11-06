using Fusion;
using Fusion.Sockets;
using UnityEngine;
using System;
using System.Collections.Generic;

public class PlayerSpawner : MonoBehaviour, INetworkRunnerCallbacks
{
    [SerializeField] public NetworkPrefabRef playerPrefab;

    private NetworkRunner _runner;

    // ✅ LobbySceneで StartGame 直後に呼ばれる
    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        Debug.Log($"[PlayerSpawner] OnPlayerJoined (LobbyScene): {player}");
    }

    // ✅ GameScene 読み込み完了時に必ず呼ばれる（Fusion2では最重要）
    public void OnSceneLoadDone(NetworkRunner runner)
    {
        Debug.Log("[PlayerSpawner] OnSceneLoadDone → GameSceneでSpawn開始");

        _runner = runner;

        if (runner.IsServer)
        {
            foreach (var p in runner.ActivePlayers)
            {
                Debug.Log($"[PlayerSpawner] Spawn PlayerPrefab for {p}");
                runner.Spawn(playerPrefab, Vector3.zero, Quaternion.identity, p);
            }
        }
    }

    // ===== 以下は空実装でOK =====
    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }
    public void OnConnectedToServer(NetworkRunner runner) { }
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
    public void OnInput(NetworkRunner runner, NetworkInput input) { }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput missing) { }
    public void OnShutdown(NetworkRunner runner, ShutdownReason reason) { }
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnSceneLoadStart(NetworkRunner runner) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
}
