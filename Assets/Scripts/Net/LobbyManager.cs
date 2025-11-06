using Fusion;
using Fusion.Sockets;
using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using static System.Net.Mime.MediaTypeNames;

/// <summary>
/// Photon Fusion2対応ロビー管理クラス
/// Host/Client名前同期をLobbyNetwork経由で実施
/// </summary>
public class LobbyManager : MonoBehaviour, INetworkRunnerCallbacks
{
    [Header("Main UI References")]
    public TMP_Text textRoomID;
    public TMP_Text textStatus;
    public Button buttonCreate;
    public Button buttonEnterRoom;
    public Button buttonTurnOrder;
    public TMP_Text textTurnOrder;
    public Button buttonStart;
    public Button buttonBackToMain;
    public TMP_Text textHostName;
    public TMP_Text textClientName;
    public Button buttonReady;

    [Header("Join Panel")]
    public GameObject joinPanel;
    public TMP_InputField inputRoomID;
    public Button buttonJoin;
    public Button buttonCancelJoin;

    // ---- Network関係 ----
    private NetworkRunner runner;
    private GameObject runnerObject;
    private LobbyNetwork lobbyNetwork;

    public static LobbyManager Instance { get; private set; }

    public enum TurnOrderMode { Random, First, Second }
    private TurnOrderMode currentTurnMode = TurnOrderMode.Random;
    public static int SelectedTurnMode = 0;

    private string currentRoomID = "";
    private bool isHost = false;
    private string myName = "";
    private string hostName = "";
    private string clientName = "";

    // LobbyNetworkのPrefab参照
    [Header("Prefabs")]
    public NetworkPrefabRef playerPrefab;
    [SerializeField] private NetworkPrefabRef lobbyNetworkPrefab;

    private bool isClientReady = false;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        SetupInitialUI();

        buttonCreate.onClick.AddListener(OnCreateRoom);
        buttonEnterRoom.onClick.AddListener(OnEnterRoomMenu);
        buttonCancelJoin.onClick.AddListener(OnCancelJoin);
        buttonJoin.onClick.AddListener(OnJoinRoom);
        buttonTurnOrder.onClick.AddListener(OnSwitchTurnOrder);
        buttonBackToMain.onClick.AddListener(OnBackToMain);
        buttonReady.onClick.AddListener(OnToggleReady);
        buttonStart.onClick.AddListener(OnStartGame);

        myName = "Player_" + UnityEngine.Random.Range(1000, 9999);
    }

    private void SetupInitialUI()
    {
        textRoomID.gameObject.SetActive(false);
        textTurnOrder.gameObject.SetActive(false);
        buttonTurnOrder.gameObject.SetActive(false);
        buttonStart.gameObject.SetActive(false);
        buttonBackToMain.gameObject.SetActive(false);
        textHostName.gameObject.SetActive(false);
        textClientName.gameObject.SetActive(false);
        buttonReady.gameObject.SetActive(false);
        joinPanel.SetActive(false);

        buttonCreate.gameObject.SetActive(true);
        buttonEnterRoom.gameObject.SetActive(true);

        textStatus.text = "";
    }

    // -------------------------------
    // Hostルーム作成
    // -------------------------------
    private async void OnCreateRoom()
    {
        isHost = true;
        textStatus.text = "ルーム作成中...";

        runnerObject = new GameObject("NetworkRunnerObject");
        runner = runnerObject.AddComponent<NetworkRunner>();
        runner.ProvideInput = true;
        var spawner = runnerObject.AddComponent<PlayerSpawner>();
        spawner.playerPrefab = playerPrefab;
        runner.AddCallbacks(spawner);
        runner.AddCallbacks(this);
        var sceneManager = runnerObject.AddComponent<NetworkSceneManagerDefault>();

        currentRoomID = GenerateRoomID(6);
        var result = await runner.StartGame(new StartGameArgs()
        {
            GameMode = GameMode.Host,
            SessionName = currentRoomID,
            SceneManager = sceneManager
        });

        if (result.Ok)
        {
            textStatus.text = "ルーム作成完了";
            hostName = myName;

            // NetworkPrefabをSpawn
            var obj = runner.Spawn(lobbyNetworkPrefab);
            lobbyNetwork = obj.GetComponent<LobbyNetwork>();

            // LobbyNetwork参照を全体で共有できるようにstatic登録
            LobbyNetwork.Instance = lobbyNetwork;

            textHostName.text = $"Host: {hostName}";
            textClientName.text = "";
            textRoomID.text = $"Room ID: {currentRoomID}";

            buttonCreate.gameObject.SetActive(false);
            buttonEnterRoom.gameObject.SetActive(false);
            textRoomID.gameObject.SetActive(true);
            textTurnOrder.gameObject.SetActive(true);
            buttonTurnOrder.gameObject.SetActive(true);
            buttonStart.gameObject.SetActive(true);
            buttonBackToMain.gameObject.SetActive(true);
            textHostName.gameObject.SetActive(true);
            textClientName.gameObject.SetActive(true);

            UpdateTurnOrderText();
            buttonStart.interactable = false;
        }
        else
        {
            textStatus.text = $"ルーム作成失敗: {result.ShutdownReason}";
        }
    }

    // -------------------------------
    // Client参加処理
    // -------------------------------
    private async void OnJoinRoom()
    {
        string roomId = inputRoomID.text.Trim();
        if (string.IsNullOrEmpty(roomId))
        {
            textStatus.text = "ルームIDを入力してください。";
            return;
        }

        textStatus.text = "ルーム参加中...";
        joinPanel.SetActive(false);
        isHost = false;

        runnerObject = new GameObject("NetworkRunnerObject");
        runner = runnerObject.AddComponent<NetworkRunner>();
        runner.ProvideInput = true;
        var spawner = runnerObject.AddComponent<PlayerSpawner>();
        spawner.playerPrefab = playerPrefab;
        runner.AddCallbacks(spawner);
        runner.AddCallbacks(this);
        var sceneManager = runnerObject.AddComponent<NetworkSceneManagerDefault>();

        var result = await runner.StartGame(new StartGameArgs()
        {
            GameMode = GameMode.Client,
            SessionName = roomId,
            SceneManager = sceneManager
        });

        if (result.Ok)
        {
            textStatus.text = "ルーム参加成功！";
            textRoomID.text = $"Room ID: {roomId}";
            textClientName.text = $"You: {myName}";

            buttonCreate.gameObject.SetActive(false);
            buttonEnterRoom.gameObject.SetActive(false);
            joinPanel.SetActive(false);

            textRoomID.gameObject.SetActive(true);
            textTurnOrder.gameObject.SetActive(true);
            textHostName.gameObject.SetActive(true);
            textClientName.gameObject.SetActive(true);
            buttonReady.gameObject.SetActive(true);

            buttonReady.GetComponentInChildren<TMP_Text>().text = "準備完了";

            // --- LobbyNetworkを再取得 ---
            await System.Threading.Tasks.Task.Delay(500);
            lobbyNetwork = FindObjectOfType<LobbyNetwork>();
            if (lobbyNetwork == null && LobbyNetwork.Instance != null)
                lobbyNetwork = LobbyNetwork.Instance;

            if (lobbyNetwork != null)
                lobbyNetwork.RPC_SendClientName(myName);
            else
                Debug.LogWarning("Client側でLobbyNetworkが見つかりませんでした");
        }
        else
        {
            textStatus.text = $"ルーム参加失敗: {result.ShutdownReason}";
            SetupInitialUI();
        }
    }

    private void SendClientNameToHost()
    {
        if (lobbyNetwork == null)
        {
            // Client側はLobbyNetworkを自動で見つける
            lobbyNetwork = FindObjectOfType<LobbyNetwork>();
        }

        if (lobbyNetwork != null)
        {
            lobbyNetwork.RPC_SendClientName(myName);
        }
        else
        {
            Debug.LogWarning("LobbyNetworkが見つかりませんでした");
        }
    }

    // -------------------------------
    // PlayerJoinedコールバック
    // -------------------------------
    public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
    {
        Debug.Log($"プレイヤー参加: {player}");
        if (isHost && lobbyNetwork != null)
        {
            // Hostが自分の名前を全員に送信
            lobbyNetwork.RPC_SendHostName(hostName);
            textClientName.text = "接続中...";
        }
    }

    // -------------------------------
    // 名前受信
    // -------------------------------
    public void OnReceiveHostName(string name)
    {
        hostName = name;
        textHostName.text = $"Host: {hostName}";
    }

    public void OnReceiveClientName(string name)
    {
        clientName = name;
        textClientName.text = $"Client: {clientName}";
    }

    // -------------------------------
    // 各種ボタン系
    // -------------------------------
    private void OnEnterRoomMenu()
    {
        joinPanel.SetActive(true);
        buttonCreate.gameObject.SetActive(false);
        buttonEnterRoom.gameObject.SetActive(false);
    }

    private void OnCancelJoin()
    {
        joinPanel.SetActive(false);
        buttonCreate.gameObject.SetActive(true);
        buttonEnterRoom.gameObject.SetActive(true);
    }

    private void OnSwitchTurnOrder()
    {
        currentTurnMode = (TurnOrderMode)(((int)currentTurnMode + 1) % Enum.GetNames(typeof(TurnOrderMode)).Length);
        SelectedTurnMode = (int)currentTurnMode;
        UpdateTurnOrderText();
    }

    private void UpdateTurnOrderText()
    {
        switch (currentTurnMode)
        {
            case TurnOrderMode.Random:
                textTurnOrder.text = "ランダム";
                break;
            case TurnOrderMode.First:
                textTurnOrder.text = "先攻:Host";
                break;
            case TurnOrderMode.Second:
                textTurnOrder.text = "先攻:Client";
                break;
        }
        if (isHost && lobbyNetwork != null)
            lobbyNetwork.RPC_SetTurnOrderText(textTurnOrder.text);
    }
    public void OnReceiveTurnOrderText(string text)
    {
        textTurnOrder.text = text;
    }

    private async void OnBackToMain()
    {
        if (runner != null)
        {
            await runner.Shutdown();
            Destroy(runnerObject);
        }
        SetupInitialUI();
        textStatus.text = "ルームを解体しました。";
    }

    private void OnToggleReady()
    {
        isClientReady = !isClientReady;
        string label = isClientReady ? "完了取り消し" : "準備完了";
        buttonReady.GetComponentInChildren<TMP_Text>().text = label;

        // RPCで他プレイヤーに通知
        if (lobbyNetwork != null)
            lobbyNetwork.RPC_SetClientReady(isClientReady);
    }

    public void OnClientReadyChanged(bool ready)
    {
        isClientReady = ready;

        if (isHost)
        {
            // Host側：Clientが準備完了ならStartを有効化
            buttonStart.interactable = ready;
        }
        else
        {
            // Client側：ボタンのテキストを同期（Hostが再送信した時にも対応）
            string label = ready ? "完了取り消し" : "準備完了";
            buttonReady.GetComponentInChildren<TMP_Text>().text = label;
        }
    }
    private void OnStartGame()
    {
        if (!isHost || !isClientReady)
            return;

        // ★ Fusion方式で全員をGameSceneに移行させる
        if (runner != null)
        {
            // すべての接続プレイヤーでシーンロードを同期
            runner.LoadScene("GameScene");
        }
    }



    private string GenerateRoomID(int length)
    {
        const string chars = "0123456789";
        StringBuilder sb = new StringBuilder();
        System.Random rand = new System.Random();
        for (int i = 0; i < length; i++)
            sb.Append(chars[rand.Next(chars.Length)]);
        return sb.ToString();
    }

    // -------------------------------
    // INetworkRunnerCallbacks 必須実装
    // -------------------------------
    public void OnConnectedToServer(NetworkRunner runner) { }
    public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
    public void OnPlayerLeft(NetworkRunner runner, PlayerRef player) { }
    public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason) { }
    public void OnDisconnectedFromServer(NetworkRunner runner, NetDisconnectReason reason) { }
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
