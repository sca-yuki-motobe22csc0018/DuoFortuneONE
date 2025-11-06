using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Fusion;

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance;

    [Header("Gameplay Managers (Scene Common)")]
    public DeckManager deckManager;
    public DiscardManager discardManager;
    public BattleManager battleManager;

    [Header("UI (Scene Common)")]
    public EffectProcessWindow effectWindow;
    public BlockWindow blockWindow;
    public DefenceWindow defenceWindow;
    public CardDetailPanel detailPanel;

    [Header("Turn UI")]
    public TMP_Text turnInfoText;
    public GameObject turnChoicePanel;
    public Button drawButton;
    public Button increaseManaButton;
    public Button endTurnButton;

    [Header("Initial Settings")]
    public int initialHandCount = 5;
    public int initialLifeCount = 3;

    // --- プレイヤーPrefab ---
    [SerializeField] private NetworkPrefabRef playerPrefab;

    // --- Prefab生成したプレイヤー格納（0=Host、1=Client） ---
    public List<PlayerManager> players = new List<PlayerManager>();

    [Networked] public int FirstPlayerIndex { get; private set; } = -1;
    [Networked] public int currentPlayerIndex { get; set; }

    private int _prevPlayerIndex = -1;
    private int turnNumber = 1;

    private NetworkRunner runner;

    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        runner = FindAnyObjectByType<NetworkRunner>();

        if (turnChoicePanel != null) turnChoicePanel.SetActive(false);
    }

    // ============================================================
    //  PlayerPrefab から呼ばれるプレイヤー登録
    // ============================================================
    public void RegisterPlayer(PlayerManager pm)
    {
        if (!players.Contains(pm))
            players.Add(pm);

        Debug.Log($"[GameManager] RegisterPlayer: 現在 {players.Count}人");

        // ✅ 2人揃ったら1回だけゲーム開始
        if (players.Count == 2)
        {
            // 相手参照セット
            players[0].SetOpponent(players[1]);
            players[1].SetOpponent(players[0]);

            // ✅ UIリスナー登録
            drawButton.onClick.AddListener(OnDrawSelected);
            increaseManaButton.onClick.AddListener(OnIncreaseManaSelected);
            endTurnButton.onClick.AddListener(OnEndTurn);

            // ✅ ゲーム開始は1回だけ
            StartCoroutine(InitGameCoroutine());
        }
    }

    // ============================================================
    //  ゲーム開始処理（2人揃ったら呼ばれる）
    // ============================================================
    private IEnumerator InitGameCoroutine()
    {
        // 山札初期化（共通）
        if (deckManager != null)
            deckManager.InitializeDeck();

        // ライフセット
        foreach (var p in players)
        {
            if (p.lifeManager != null)
                p.lifeManager.SetupInitialLife(initialLifeCount, deckManager);
        }

        // 初期手札ドロー
        for (int i = 0; i < initialHandCount; i++)
        {
            deckManager.DrawCardToHand(players[0]);
            deckManager.DrawCardToHand(players[1]);
        }

        // --- Host側で先攻決定 ---
        if (Object.HasStateAuthority)
        {
            int mode = LobbyManager.SelectedTurnMode;

            switch (mode)
            {
                case 0:
                    FirstPlayerIndex = Random.Range(0, 2);
                    Debug.Log($"[Host] ランダム先攻 → {FirstPlayerIndex}");
                    break;

                case 1:
                    FirstPlayerIndex = 0;
                    break;

                case 2:
                    FirstPlayerIndex = 1;
                    break;
            }

            currentPlayerIndex = FirstPlayerIndex;
        }

        yield return new WaitForSeconds(0.2f);

        StartTurnInternal();
    }

    // ============================================================
    //  ターン開始処理（UIなどの切替）
    // ============================================================
    private void StartTurnInternal()
    {
        var player = players[currentPlayerIndex];

        // UI表示
        if (turnInfoText != null)
        {
            string text = player.Object.HasInputAuthority ?
                "あなたのターン" : "相手のターン";

            turnInfoText.text = $"Turn {turnNumber}: {text}";
        }

        // ✅ 自分のターンだけ UI ON
        if (player.Object.HasInputAuthority)
        {
            if (turnChoicePanel != null) turnChoicePanel.SetActive(true);
            if (endTurnButton != null) endTurnButton.interactable = true;

            if (effectWindow != null)
                StartCoroutine(effectWindow.ShowProcess("あなたのターン"));
        }
        else
        {
            if (turnChoicePanel != null) turnChoicePanel.SetActive(false);
            if (endTurnButton != null) endTurnButton.interactable = false;
        }
    }

    // ============================================================
    //  UI ボタン処理
    // ============================================================

    private void OnDrawSelected()
    {
        var player = players[currentPlayerIndex];
        if (!player.Object.HasInputAuthority) return;

        deckManager.DrawCardToHand(player);
        deckManager.DrawCardToHand(player);

        player.ResetMana();
        turnChoicePanel.SetActive(false);
    }

    private void OnIncreaseManaSelected()
    {
        var player = players[currentPlayerIndex];
        if (!player.Object.HasInputAuthority) return;

        player.IncreaseMaxMana(1);
        player.ResetMana();

        turnChoicePanel.SetActive(false);
    }

    public void OnEndTurn()
    {
        var player = players[currentPlayerIndex];
        if (!player.Object.HasInputAuthority) return;

        turnChoicePanel.SetActive(false);
        endTurnButton.interactable = false;

        if (Object.HasStateAuthority)
            NextTurn();
        else
            Rpc_RequestNextTurn();
    }

    // ============================================================
    //  ターンを進める（Hostだけ）
    // ============================================================
    private void NextTurn()
    {
        if (!Object.HasStateAuthority) return;

        currentPlayerIndex = (currentPlayerIndex == 0) ? 1 : 0;

        if (currentPlayerIndex == 0)
            turnNumber++;
    }

    [Rpc(sources: RpcSources.All, targets: RpcTargets.StateAuthority)]
    private void Rpc_RequestNextTurn()
    {
        NextTurn();
    }

    // ============================================================
    //  Fusion 推奨：RenderでNetworkedの変更監視
    // ============================================================
    public override void Render()
    {
        if (players.Count < 2) return;

        if (currentPlayerIndex != _prevPlayerIndex)
        {
            _prevPlayerIndex = currentPlayerIndex;
            StartTurnInternal();
        }
    }
}
