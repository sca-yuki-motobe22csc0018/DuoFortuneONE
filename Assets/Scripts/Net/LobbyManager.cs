using System;
using System.Collections.Generic;
using System.Text;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

public class LobbyManager : MonoBehaviour, INetworkRunnerCallbacks
{
    [Header("UI References")]
    public TMP_InputField inputRoomID;
    public TMP_Text textRoomID;
    public TMP_Text textStatus;
    public Button buttonCreate;
    public Button buttonJoin;

    private NetworkRunner runner;
    private GameObject runnerObject;
    private string currentRoomID = "";

    private void Start()
    {
        if (textRoomID != null)
            textRoomID.text = "";

        buttonCreate.onClick.AddListener(() => StartGame(isHost: true));
        buttonJoin.onClick.AddListener(() => StartGame(isHost: false));
    }

    private string GenerateRoomID(int length)
    {
        const string chars = "0123456789";
        StringBuilder sb = new StringBuilder(length);
        System.Random rand = new System.Random();

        for (int i = 0; i < length; i++)
            sb.Append(chars[rand.Next(chars.Length)]);

        return sb.ToString();
    }

    private async void StartGame(bool isHost)
    {
        // ✅ Runnerが既に存在していれば安全に削除
        if (runnerObject != null)
        {
            textStatus.text = "再接続準備中...";
            if (runner != null)
            {
                try
                {
                    await runner.Shutdown();
                }
                catch { }
            }

            Destroy(runnerObject);
            runnerObject = null;
            runner = null;
            await System.Threading.Tasks.Task.Delay(400);
        }

        // ✅ Runner専用オブジェクトを新しく生成
        runnerObject = new GameObject("NetworkRunnerObject");
        runner = runnerObject.AddComponent<NetworkRunner>();
        runner.ProvideInput = true;

        var sceneManager = runnerObject.AddComponent<NetworkSceneManagerDefault>();
        runner.AddCallbacks(this);

        string roomId;

        if (isHost)
        {
            currentRoomID = GenerateRoomID(6);
            roomId = currentRoomID;
            if (textRoomID != null)
                textRoomID.text = $"Room ID: {roomId}";
        }
        else
        {
            roomId = inputRoomID.text.Trim();
            if (string.IsNullOrEmpty(roomId))
            {
                textStatus.text = "ルームIDを入力してください。";
                return;
            }
            if (textRoomID != null)
                textRoomID.text = "";
        }

        textStatus.text = isHost
            ? $"ルーム作成中 ({roomId})..."
            : $"ルーム参加中 ({roomId})...";

        var result = await runner.StartGame(new StartGameArgs
        {
            GameMode = isHost ? GameMode.Host : GameMode.Client,
            SessionName = roomId,
            SceneManager = sceneManager
        });

        if (result.Ok)
        {
            textStatus.text = isHost
                ? $"ルーム作成成功！\nRoom ID: {roomId}"
                : $"ルーム参加成功！";

            Debug.Log($"[LobbyManager] {roomId} に {(isHost ? "Host" : "Client")}として接続成功");

            await System.Threading.Tasks.Task.Delay(1500);
            SceneManager.LoadScene("GameScene");
        }
        else
        {
            textStatus.text = $"接続失敗: {result.ShutdownReason}\nIDを確認して再試行してください。";
            Debug.LogError($"[LobbyManager] StartGame失敗: {result.ShutdownReason}");

            // ✅ Runnerオブジェクトを破棄して次の接続を許可
            if (runnerObject != null)
                Destroy(runnerObject);

            runner = null;
            runnerObject = null;
        }
    }

    // ======================================
    // INetworkRunnerCallbacks
    // ======================================
    public void OnConnectedToServer(NetworkRunner runner) => Debug.Log("サーバーに接続しました。");
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason)
        => Debug.LogError($"接続失敗: {reason}");
    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
        => Debug.Log($"プレイヤー参加: {player}");
    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
        => Debug.Log($"プレイヤー退出: {player}");
    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
        => Debug.Log($"Runner停止: {shutdownReason}");
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason)
        => Debug.Log($"サーバーから切断: {reason}");
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
