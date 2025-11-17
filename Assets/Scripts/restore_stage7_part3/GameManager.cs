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
        // Host だけが山札と先攻決定・配布を担当
        if (Object.HasStateAuthority)
        {
            if (deckManager != null)
                deckManager.InitializeDeck();

            // --- 先攻決定 ---
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

            int first = FirstPlayerIndex;
            int second = (first == 0) ? 1 : 0;

            // 山札から ID を抜き取る（先攻5 → 後攻5 → 先攻Life3 → 後攻Life3）
            int[] firstHandIDs = deckManager.DrawCardIDs(initialHandCount);
            int[] secondHandIDs = deckManager.DrawCardIDs(initialHandCount);
            int[] firstLifeIDs = deckManager.DrawCardIDs(initialLifeCount);
            int[] secondLifeIDs = deckManager.DrawCardIDs(initialLifeCount);

            // players[0], players[1] に対応する形に並べ替え
            int[] p0Hand = (first == 0) ? firstHandIDs : secondHandIDs;
            int[] p1Hand = (first == 0) ? secondHandIDs : firstHandIDs;
            int[] p0Life = (first == 0) ? firstLifeIDs : secondLifeIDs;
            int[] p1Life = (first == 0) ? secondLifeIDs : firstLifeIDs;

            // ★ 全員に初期手札＆ライフを反映
            RPC_InitHandsAndLife(p0Hand, p1Hand, p0Life, p1Life);
        }

        // ちょっと待ってからターン開始（RPC反映待ち）
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
        if (runner == null)
            runner = FindAnyObjectByType<NetworkRunner>();

        if (runner == null)
        {
            Debug.LogError("OnDrawSelected: NetworkRunner が見つかりません。");
            return;
        }

        // ★ ローカルで勝手に引かず、「引きたい」とHostにお願いする
        RPC_RequestDraw(runner.LocalPlayer);

        // もし「ドロー or チャージの選択ウィンドウ」を閉じる処理があればここでやる
        if (turnChoicePanel != null)
            turnChoicePanel.SetActive(false);

        // このあと、メインフェイズ開始やフラグの更新があるなら、それはそのままでOK
        // 例: isFirstTurnChoiceDone = true; とか
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
    //  初期手札＆ライフ配布用 RPC（Host → 全員）
    // ============================================================
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_InitHandsAndLife(int[] p0Hand, int[] p1Hand, int[] p0Life, int[] p1Life)
    {
        if (players.Count < 2)
        {
            Debug.LogWarning("RPC_InitHandsAndLife: プレイヤーが2人揃っていません。");
            return;
        }

        if (deckManager == null)
        {
            Debug.LogError("RPC_InitHandsAndLife: deckManager が設定されていません。");
            return;
        }

        var p0 = players[0];
        var p1 = players[1];

        // ---------- 手札 ----------
        if (p0.handManager != null)
        {
            foreach (var id in p0Hand)
            {
                var data = deckManager.GetCardDataById(id);
                p0.handManager.AddCardFromData(data);
            }
        }

        if (p1.handManager != null)
        {
            foreach (var id in p1Hand)
            {
                var data = deckManager.GetCardDataById(id);
                p1.handManager.AddCardFromData(data);
            }
        }

        // ---------- ライフ ----------
        if (p0.lifeManager != null)
        {
            foreach (var id in p0Life)
            {
                var data = deckManager.GetCardDataById(id);
                p0.lifeManager.AddLife(data);
            }
        }

        if (p1.lifeManager != null)
        {
            foreach (var id in p1Life)
            {
                var data = deckManager.GetCardDataById(id);
                p1.lifeManager.AddLife(data);
            }
        }

        Debug.Log("RPC_InitHandsAndLife: 初期手札＆ライフを反映しました。");
    }

    // プレイヤーからのドロー要求（クライアント → Host）
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestDraw(PlayerRef requester)
    {
        if (players.Count < 2 || deckManager == null)
            return;

        // requester がどの index のプレイヤーかを特定
        int playerIndex = -1;
        for (int i = 0; i < players.Count; i++)
        {
            var pm = players[i];
            if (pm != null && pm.Object != null && pm.Object.InputAuthority == requester)
            {
                playerIndex = i;
                break;
            }
        }

        if (playerIndex < 0)
        {
            Debug.LogWarning($"RPC_RequestDraw: requester {requester} に対応する PlayerManager が見つかりません。");
            return;
        }

        // 今のターンのプレイヤー以外からの要求は無視
        if (playerIndex != currentPlayerIndex)
        {
            Debug.Log($"RPC_RequestDraw: ターンプレイヤーではないため無視します。 index={playerIndex}, current={currentPlayerIndex}");
            return;
        }

        // 山札から1枚IDを引く（Host だけ山札を触る）
        int[] ids = deckManager.DrawCardIDs(1);
        if (ids.Length == 0)
        {
            Debug.Log("RPC_RequestDraw: 山札が空です。");
            return;
        }

        int cardId = ids[0];

        // 全員に「playerIndex が cardId を引いた」と通知
        RPC_ApplyDraw(playerIndex, cardId);
    }

    // 実際に手札にカードを追加するRPC（Host → 全員）
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ApplyDraw(int playerIndex, int cardId)
    {
        if (deckManager == null)
        {
            Debug.LogError("RPC_ApplyDraw: deckManager が設定されていません。");
            return;
        }

        if (playerIndex < 0 || playerIndex >= players.Count)
        {
            Debug.LogWarning($"RPC_ApplyDraw: 不正な playerIndex={playerIndex}");
            return;
        }

        var pm = players[playerIndex];
        if (pm == null || pm.handManager == null)
        {
            Debug.LogWarning("RPC_ApplyDraw: PlayerManager または handManager がありません。");
            return;
        }

        // IDからカードデータを復元して手札に追加
        var data = deckManager.GetCardDataById(cardId);
        pm.handManager.AddCardFromData(data);

        Debug.Log($"RPC_ApplyDraw: playerIndex={playerIndex} に cardID={cardId} をドローさせました。");
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
