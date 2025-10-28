using System;
using System.Collections.Generic;
using System.Text;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Photon Fusion ロビー管理スクリプト（v3）
/// ・ルームIDは数字のみ（0-9）
/// ・接続失敗時は再試行可能
/// ・ルームIDは「Create Room」を押した時のみ表示
/// </summary>
public class LobbyManager : MonoBehaviour, INetworkRunnerCallbacks
{
    [Header("UI References")]
    public TMP_InputField inputRoomID;   // 手入力用のルームID欄
    public TMP_Text textRoomID;          // 自動生成されたルームIDを表示
    public TMP_Text textStatus;          // 状態表示テキスト
    public Button buttonCreate;          // ルーム作成ボタン
    public Button buttonJoin;            // ルーム参加ボタン

    private NetworkRunner runner;
    private string currentRoomID = "";

    private void Start()
    {
        // 起動時はRoom IDを非表示に
        if (textRoomID != null)
            textRoomID.text = "";

        // ボタンイベント登録
        buttonCreate.onClick.AddListener(() => StartGame(isHost: true));
        buttonJoin.onClick.AddListener(() => StartGame(isHost: false));
    }

    /// <summary>
    /// ルームIDを数字のみで生成（0～9）
    /// </summary>
    private string GenerateRoomID(int length)
    {
        const string chars = "0123456789";
        StringBuilder sb = new StringBuilder(length);
        System.Random rand = new System.Random();

        for (int i = 0; i < length; i++)
            sb.Append(chars[rand.Next(chars.Length)]);

        return sb.ToString();
    }

    /// <summary>
    /// ゲーム開始（Host or Client）
    /// </summary>
    private async void StartGame(bool isHost)
    {
        // 既存のRunnerがある場合は停止して再試行
        if (runner != null)
        {
            textStatus.text = "再接続準備中...";
            await runner.Shutdown();
            runner = null;
            await System.Threading.Tasks.Task.Delay(300);
        }

        runner = gameObject.AddComponent<NetworkRunner>();
        runner.ProvideInput = true;

        var sceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>();

        string roomId;

        if (isHost)
        {
            // Hostは新しいIDを生成
            currentRoomID = GenerateRoomID(6);
            roomId = currentRoomID;

            // ルームIDを表示（Hostのみ）
            if (textRoomID != null)
                textRoomID.text = $"Room ID: {roomId}";
        }
        else
        {
            // Clientは入力欄から取得
            roomId = inputRoomID.text.Trim();
            if (string.IsNullOrEmpty(roomId))
            {
                textStatus.text = "ルームIDを入力してください。";
                return;
            }

            // 参加側はRoom ID表示しない
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

            // 数秒後にGameSceneへ遷移
            await System.Threading.Tasks.Task.Delay(1500);
            SceneManager.LoadScene("GameScene");
        }
        else
        {
            textStatus.text = $"接続失敗: {result.ShutdownReason}\nIDを確認して再試行してください。";
            Debug.LogError($"[LobbyManager] StartGame失敗: {result.ShutdownReason}");
            runner = null; // 再接続許可
        }
    }

    // ===============================
    // INetworkRunnerCallbacks
    // ===============================

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
