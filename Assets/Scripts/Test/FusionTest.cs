using Fusion;
using Fusion.Sockets;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Photon Fusion テスト用（Host⇄Client自動切り替え）
/// Unityエディタで起動するとHost、ビルド実行ファイルではClientとして動作。
/// </summary>
public class FusionTest : MonoBehaviour, INetworkRunnerCallbacks
{
    private NetworkRunner runner;

    private async void Start()
    {
        runner = gameObject.AddComponent<NetworkRunner>();
        var sceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>();

        // ★ Unity Editor内ならHost、ビルド版ならClient
#if UNITY_EDITOR
        var mode = GameMode.Host;
        Debug.Log("[FusionTest] Unity Editorで起動 → Hostとして動作");
#else
        var mode = GameMode.Client;
        Debug.Log("[FusionTest] ビルド版で起動 → Clientとして動作");
#endif

        var result = await runner.StartGame(new StartGameArgs()
        {
            GameMode = mode,
            SessionName = "TestRoom",
            SceneManager = sceneManager
        });

        if (result.Ok)
        {
            Debug.Log($"[FusionTest] {mode}として起動しました。");
        }
        else
        {
            Debug.LogError($"[FusionTest] 起動失敗: {result.ShutdownReason}");
        }
    }

    // ===============================
    // INetworkRunnerCallbacks
    // ===============================

    public void OnConnectedToServer(NetworkRunner runner)
    {
        Debug.Log("サーバーに接続しました。");
    }

    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
    {
        Debug.LogError($"接続失敗: {reason}");
    }

    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        Debug.Log($"プレイヤー参加: {player}");
    }

    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
    {
        Debug.Log($"プレイヤー退出: {player}");
    }

    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
    {
        Debug.Log($"Runner停止: {shutdownReason}");
    }

    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
    {
        Debug.Log($"サーバーから切断されました: {reason}");
    }

    public void OnInput(NetworkRunner runner, NetworkInput input) { }
    public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
    public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
    public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ReliableKey key, ArraySegment<byte> data) { }
    public void OnReliableDataProgress(NetworkRunner runner, PlayerRef player, ReliableKey key, float progress) { }
    public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }
    public void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken) { }
    public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
    public void OnSceneLoadDone(NetworkRunner runner) { }
    public void OnSceneLoadStart(NetworkRunner runner) { }
    public void OnObjectEnterAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnObjectExitAOI(NetworkRunner runner, NetworkObject obj, PlayerRef player) { }
    public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
}
