using Fusion;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static CardGenerator;
using System.Linq;
using UnityEngine.SceneManagement;




public class GameManager : NetworkBehaviour
{
    public static GameManager Instance;

    private bool _lockHandCardUse = false;

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

    [Header("Deck UI")]
    public TMP_Text deckCountText;

    // ============================
    // GameOver UI
    // ============================
    [Header("Game Over UI")]
    public GameObject gameOverPanel;
    public TMP_Text gameOverResultText;   // "WIN / LOSE / DRAW"
    public TMP_Text gameOverReasonText;   // 理由（任意）
    public Button returnHomeButton;
    public string homeSceneName = "Title"; // ←あなたのホームScene名に合わせる


    // ▼追加：勝敗状態
    private bool _isGameOver = false;
    public bool IsGameOver => _isGameOver;

    private const string EX_CARD_NAME = "EX_001";
    private const int EX_WIN_COUNT = 5;

    private const string REASON_KEY_RETIRE = "__RETIRE__";

    [Header("EX_001 Win Cut-in")]
    public GameObject ex001CutinPanel;          // 5枚Imageが並んでるパネル（位置は既に配置済みでOK）
    public Image[] ex001CutinImages = new Image[5];

    public float ex001FadeDuration = 0.1f;      // 1枚のフェード時間
    public float ex001StepInterval = 0.05f;     // 左→右の間隔（シャシャシャ感）
    public float ex001WaitSeconds = 10f;        // クリックがなければ自動で勝敗画面へ

    public float ex001FlipDuration = 0.25f;     // 負け側：相手手札のフリップ速度（1枚）
    public float ex001FlipInterval = 0.05f;     // フリップの間隔



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

    // ============================================================
    //  Processing Card Bar (Host authoritative)
    // ============================================================
    private int _nextProcessingId = 0;

    // (PlayerRef, localToken) -> processId
    private readonly Dictionary<(PlayerRef, int), int> _processingTokenMap = new Dictionary<(PlayerRef, int), int>();


    private NetworkRunner runner;

    // ============================================================
    // Host only: hand id list (authoritative)
    // ============================================================
    private List<int> hostHandIdsP0 = new List<int>();
    private List<int> hostHandIdsP1 = new List<int>();

    // ============================================================
    // Deck Sync (Host builds deck after receiving both selections)
    // ============================================================
    private bool _sentLocalDeckToHost = false;

    private bool _hostReceivedDeckP0 = false;
    private bool _hostReceivedDeckP1 = false;

    private List<int> _hostDeckIdsP0 = new List<int>(); // players[0]
    private List<int> _hostDeckIdsP1 = new List<int>(); // players[1]

    // ============================
    // コスト宣言（そのターン中、両者が該当コストを使えない）
    // ============================
    private readonly System.Collections.Generic.List<int> _sealedCostsThisTurn
        = new System.Collections.Generic.List<int>();

    // ============================
    // ライフゾーンDEFENCE封印（永続）
    // ============================
    private bool _lifeDefenceSealedP0 = false;
    private bool _lifeDefenceSealedP1 = false;




    private bool _sealSessionActive = false;
    private bool _sealSessionExpectBoth = false;
    private int _sealSessionId = 0;

    private bool _sealSubmittedP0 = false;
    private bool _sealSubmittedP1 = false;
    private int _sealChoiceP0 = -1;
    private int _sealChoiceP1 = -1;

    // クライアント側待機用（ローカル）
    private int _localSealSessionId = -1;
    private int _localSealResolvedSessionId = -1;

    // ===== 捨て札回収 同期完了待ち（ローカル）=====
    private int _localRecoverResolvedSessionId = -1;
    public int LocalRecoverResolvedSessionId => _localRecoverResolvedSessionId;


    public bool IsCostSealed(int cost)
    {
        return _sealedCostsThisTurn.Contains(cost);
    }

    public int LocalSealSessionId => _localSealSessionId;
    public int LocalSealResolvedSessionId => _localSealResolvedSessionId;

    private List<int> GetLocalSelectedDeckIds()
    {
        List<string> src = null;

        // ① シーン跨ぎ用（推奨）
        if (GlobalDeckLocator.Instance != null && GlobalDeckLocator.Instance.selectedDeck != null && GlobalDeckLocator.Instance.selectedDeck.Count > 0)
        {
            src = GlobalDeckLocator.Instance.selectedDeck;
        }
        else
        {
            // ② JSON保存から取得
            if (DeckSaveManager.Instance != null)
            {
                var deck = DeckSaveManager.Instance.GetSelectedDeck();
                if (deck != null && deck.cardNumbers != null && deck.cardNumbers.Count > 0)
                    src = deck.cardNumbers;
            }
        }

        var ids = new List<int>();
        if (src == null) return ids;

        for (int i = 0; i < src.Count; i++)
        {
            if (int.TryParse(src[i], out int id))
                ids.Add(id);
        }

        return ids;
    }
    private void TrySendLocalDeckToHost()
    {
        if (_sentLocalDeckToHost) return;

        if (runner == null) runner = FindAnyObjectByType<NetworkRunner>();
        if (runner == null) return;

        // Hostは送らない（Hostはローカル取得で確定させる）
        if (Object.HasStateAuthority) return;

        var ids = GetLocalSelectedDeckIds();
        RPC_SendSelectedDeckToHost(runner.LocalPlayer, ids.ToArray());
        _sentLocalDeckToHost = true;

        Debug.Log($"[GameManager] Sent local deck to host. count={ids.Count}");
    }
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_SendSelectedDeckToHost(PlayerRef sender, int[] deckIds)
    {
        if (players == null || players.Count < 2) return;

        int idx = -1;
        for (int i = 0; i < players.Count; i++)
        {
            var pm = players[i];
            if (pm != null && pm.Object != null && pm.Object.InputAuthority == sender)
            {
                idx = i;
                break;
            }
        }
        if (idx < 0) return;

        if (deckIds == null) deckIds = new int[0];

        if (idx == 0)
        {
            _hostDeckIdsP0 = new List<int>(deckIds);
            _hostReceivedDeckP0 = true;
        }
        else
        {
            _hostDeckIdsP1 = new List<int>(deckIds);
            _hostReceivedDeckP1 = true;
        }

        Debug.Log($"[Host] Received deck from player[{idx}] count={deckIds.Length}");
    }




    private List<int> GetHostHandIdList(int playerIndex)
    {
        return (playerIndex == 0) ? hostHandIdsP0 : hostHandIdsP1;
    }


    private void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        runner = FindAnyObjectByType<NetworkRunner>();
        // ★メインBGM開始（対戦シーンで1回）
        TryStartMainBgm();


        if (turnChoicePanel != null) turnChoicePanel.SetActive(false);

        // ▼追加：GameOver UI 初期化
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (returnHomeButton != null)
        {
            // ★GameManagerがDespawnしてもボタンが死なないように、ローカルUIで処理する
            var local = returnHomeButton.GetComponent<ReturnHomeLocalUI>();
            if (local == null) local = returnHomeButton.gameObject.AddComponent<ReturnHomeLocalUI>();
            local.Setup(returnHomeButton, homeSceneName);
        }
    }
    private bool _didStartMainBgm = false;

    private void TryStartMainBgm()
    {
        if (_didStartMainBgm) return;

        var am = AudioManager.Instance;
        if (am == null || am.library == null) return;
        if (am.library.bgmMainGame == null) return;

        am.ChangeBgm(am.library.bgmMainGame, loop: true, volumeScale: 1f);
        _didStartMainBgm = true;
    }


    public bool IsLocalPlayersTurn()
    {
        if (players == null) return false;
        if (currentPlayerIndex < 0 || currentPlayerIndex >= players.Count) return false;

        var turnPlayer = players[currentPlayerIndex];
        if (turnPlayer == null || turnPlayer.Object == null) return false;

        var r = runner;
        if (r == null) r = FindAnyObjectByType<NetworkRunner>();
        if (r == null) return false;

        return (turnPlayer.Object.InputAuthority == r.LocalPlayer);
    }

    public bool IsHandCardUseLocked()
    {
        return _lockHandCardUse;
    }

    public void SetHandCardUseLocked(bool locked)
    {
        _lockHandCardUse = locked;

        // ★追加：ローカルのターン中だけ、ロックならEndTurnも押せない
        if (endTurnButton != null)
        {
            endTurnButton.interactable = IsLocalPlayersTurn() && !locked && !_isGameOver;
        }
    }


    // ============================================================
    //  PlayerPrefab から呼ばれるプレイヤー登録
    // ============================================================
    public void RegisterPlayer(PlayerManager pm)
    {
        if (!players.Contains(pm))
            players.Add(pm);

        Debug.Log($"[GameManager] RegisterPlayer: 現在 {players.Count}人");

        if (players.Count == 2)
        {
            players[0].SetOpponent(players[1]);
            players[1].SetOpponent(players[0]);

            drawButton.onClick.AddListener(OnDrawSelected);
            increaseManaButton.onClick.AddListener(OnIncreaseManaSelected);
            endTurnButton.onClick.AddListener(OnEndTurn);

            // ★ Clientはここで一度だけ Host にデッキを送る
            TrySendLocalDeckToHost();

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
            // ★ Host自身のデッキはローカルから確定（players[0]前提）
            //    ※もし順番がズレる可能性があるなら、sender判定式に寄せるのもOK
            _hostDeckIdsP0 = GetLocalSelectedDeckIds();
            _hostReceivedDeckP0 = true;

            // ★ Clientのデッキが届くまで待つ
            while (!(_hostReceivedDeckP0 && _hostReceivedDeckP1))
                yield return null;

            // ★ 受信した2人分で山札を作る（DefaultDeck + P0 + P1）
            if (deckManager != null)
                deckManager.InitializeDeckWithSelectedDecks(_hostDeckIdsP0, _hostDeckIdsP1);

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

            int[] firstHandIDs = deckManager.DrawCardIDs(initialHandCount);
            int[] secondHandIDs = deckManager.DrawCardIDs(initialHandCount);
            int[] firstLifeIDs = deckManager.DrawCardIDs(initialLifeCount);
            int[] secondLifeIDs = deckManager.DrawCardIDs(initialLifeCount);

            int[] p0Hand = (first == 0) ? firstHandIDs : secondHandIDs;
            int[] p1Hand = (first == 0) ? secondHandIDs : firstHandIDs;
            int[] p0Life = (first == 0) ? firstLifeIDs : secondLifeIDs;
            int[] p1Life = (first == 0) ? secondLifeIDs : firstLifeIDs;

            RPC_InitHandsAndLife(p0Hand, p1Hand, p0Life, p1Life);
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

            turnInfoText.text = $"{text}";
        }

        // ✅ 自分のターンだけ UI ON
        if (player.Object.HasInputAuthority)
        {
            if (turnChoicePanel != null) turnChoicePanel.SetActive(true);

            // ★変更：ロック中/ゲーム終了中は押せない
            if (endTurnButton != null) endTurnButton.interactable = !_lockHandCardUse && !_isGameOver;

            if (effectWindow != null)
                StartCoroutine(effectWindow.ShowProcessAuto("あなたのターン", 0.8f, false));
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

        // ★ 「このドローではマナも回復してほしい」ので true を渡す
        RPC_RequestDraw(runner.LocalPlayer, true);
        RPC_RequestDraw(runner.LocalPlayer, true);

        if (turnChoicePanel != null)
            turnChoicePanel.SetActive(false);
    }

    private void OnIncreaseManaSelected()
    {
        if (runner == null)
            runner = FindAnyObjectByType<NetworkRunner>();

        if (runner == null)
        {
            Debug.LogError("OnIncreaseManaSelected: NetworkRunner が見つかりません。");
            return;
        }

        // ★ 自分ではマナをいじらない。Hostに「チャージしたい」と伝えるだけ
        RPC_RequestCharge(runner.LocalPlayer);

        // UIは今まで通り閉じてOK
        if (turnChoicePanel != null)
            turnChoicePanel.SetActive(false);
    }

    // ★ プレイヤー index を受け取って、そのプレイヤーにチャージ処理を適用する共通関数
    private void ApplyChargeInternal(int playerIndex)
    {
        if (playerIndex < 0 || playerIndex >= players.Count)
            return;

        var player = players[playerIndex];
        if (player == null)
            return;

        // ★ ここに元の OnIncreaseManaSelected の「中身」だけ移植する
        player.IncreaseMaxMana(1);
        player.ResetMana();
    }



    public void OnEndTurn()
    {
        // ★追加：処理中/ゲーム終了後はターンを終えられない
        if (_isGameOver) return;
        if (_lockHandCardUse) return;

        var player = players[currentPlayerIndex];
        if (!player.Object.HasInputAuthority) return;

        turnChoicePanel.SetActive(false);
        endTurnButton.interactable = false;

        if (Object.HasStateAuthority)
            NextTurn();
        else
            Rpc_RequestNextTurn();
    }
    public void OnEndTurnFromEffect()
    {
        // ★効果によるターンエンドは「処理中ロック」を無視して通す
        if (_isGameOver) return;

        var player = players[currentPlayerIndex];
        if (!player.Object.HasInputAuthority) return;

        if (turnChoicePanel != null)
            turnChoicePanel.SetActive(false);

        if (endTurnButton != null)
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
        if (p0.handManager != null && p0.Object != null && p0.Object.HasInputAuthority)
        {
            foreach (var id in p0Hand)
            {
                var data = deckManager.GetCardDataById(id);
                p0.handManager.AddCardFromData(data);
            }
        }

        if (p1.handManager != null && p1.Object != null && p1.Object.HasInputAuthority)
        {
            foreach (var id in p1Hand)
            {
                var data = deckManager.GetCardDataById(id);
                p1.handManager.AddCardFromData(data);
            }
        }
        // ★ Host only: 初期手札IDリストを確定
        if (Object.HasStateAuthority)
        {
            hostHandIdsP0 = new List<int>(p0Hand);
            hostHandIdsP1 = new List<int>(p1Hand);

            if (players[0] != null) players[0].handCount = hostHandIdsP0.Count;
            if (players[1] != null) players[1].handCount = hostHandIdsP1.Count;
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

        // ★ 初期手札＆ライフ反映が終わったタイミングで、手札枚数UIを再計算
        foreach (var pm in players)
        {
            if (pm != null && pm.Object.HasInputAuthority)
            {
                pm.NotifyHandChangedForBothSides();
                pm.UpdateLifeUI();   // ★ 追加
            }
        }
        if (Object.HasStateAuthority)
        {
            // ▼追加：初期配布後に山札枚数を同期
            SyncDeckCountHostOnly();

            // ▼追加：初期手札でEX勝利してたら即決着
            TryCheckEx001Win(0);
            TryCheckEx001Win(1);
        }
    }

    // ============================================================
    //  ライフ破壊の同期（Host → 他クライアント）
    // ============================================================
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_SyncRemoveLife(int playerIndex)
    {
        // ★SFX：ライフ破壊（両者）
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySfx(SfxClipId.LifeBreak);

        // Host はすでに RemoveLife 済みなので、ここでは何もしない
        if (Object.HasStateAuthority)
            return;

        if (players == null || playerIndex < 0 || playerIndex >= players.Count)
            return;

        var pm = players[playerIndex];
        if (pm == null || pm.lifeManager == null)
            return;

        // Client側でだけ実際に削除
        pm.lifeManager.RemoveLife();
    }


    // ============================
    // ライフ追加（Host → 全員）
    // ============================

    // Host だけが呼ぶ: 指定プレイヤーに amount 枚ライフを追加
    public void AddLifeToPlayer(PlayerManager targetPlayer, int amount)
    {
        if (!Object.HasStateAuthority) return;    // Host 以外は何もしない
        if (deckManager == null) return;
        if (targetPlayer == null) return;
        if (amount <= 0) return;

        int playerIndex = players.IndexOf(targetPlayer);
        if (playerIndex < 0)
        {
            Debug.LogWarning("[GameManager.AddLifeToPlayer] targetPlayer が players に見つかりません。");
            return;
        }

        // Host だけが山札からライフ用のカードIDを引く
        int[] lifeIds = deckManager.DrawCardIDs(amount);
        if (lifeIds == null || lifeIds.Length == 0)
        {
            Debug.LogWarning("[GameManager.AddLifeToPlayer] 山札が空のためライフを追加できません。");
            return;
        }

        // 全クライアントに「このIDのカードをライフに追加しろ」と通知
        RPC_SyncAddLife(playerIndex, lifeIds);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_SyncAddLife(int playerIndex, int[] lifeIds)
    {
        if (deckManager == null) return;
        if (players == null) return;
        if (playerIndex < 0 || playerIndex >= players.Count) return;
        if (lifeIds == null || lifeIds.Length == 0) return;

        var pm = players[playerIndex];
        if (pm == null || pm.lifeManager == null) return;

        // 各クライアントで同じIDのカードデータを復元してライフに追加
        foreach (var id in lifeIds)
        {
            var data = deckManager.GetCardDataById(id);
            if (data != null)
            {
                pm.lifeManager.AddLife(data);
            }
        }

        // 自分視点のライフUIを更新
        foreach (var p in players)
        {
            if (p != null && p.Object != null && p.Object.HasInputAuthority)
            {
                p.UpdateLifeUI();
            }
        }
    }

    // ============================
    // 効果 LifeAdd 用リクエスト（Client → Host）
    // ============================
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_RequestEffectLifeAdd(PlayerRef requester, int amount)
    {
        if (amount <= 0) return;
        if (players == null || players.Count == 0) return;

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
            Debug.LogWarning($"RPC_RequestEffectLifeAdd: requester {requester} に対応する PlayerManager が見つかりません。");
            return;
        }

        // Host 側で AddLifeToPlayer を実行
        AddLifeToPlayer(players[playerIndex], amount);
    }

    public int BeginProcessingCardHost(int cardId)
    {
        if (!Object.HasStateAuthority) return -1;

        _nextProcessingId++;
        int pid = _nextProcessingId;

        RPC_AddProcessingCard(pid, cardId);
        return pid;
    }

    public void EndProcessingCardHost(int processId)
    {
        if (!Object.HasStateAuthority) return;
        if (processId < 0) return;

        RPC_RemoveProcessingCard(processId);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_RequestBeginProcessingCard(PlayerRef requester, int localToken, int cardId)
    {
        if (!Object.HasStateAuthority) return;
        if (localToken == 0) return;
        if (cardId <= 0) return;

        var key = (requester, localToken);
        if (_processingTokenMap.ContainsKey(key)) return;

        _nextProcessingId++;
        int pid = _nextProcessingId;

        _processingTokenMap[key] = pid;
        RPC_AddProcessingCard(pid, cardId);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_RequestEndProcessingCard(PlayerRef requester, int localToken)
    {
        if (!Object.HasStateAuthority) return;
        if (localToken == 0) return;

        var key = (requester, localToken);
        if (!_processingTokenMap.TryGetValue(key, out int pid)) return;

        _processingTokenMap.Remove(key);
        RPC_RemoveProcessingCard(pid);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_AddProcessingCard(int processId, int cardId)
    {
        // ★SFX：カード使用（両者）
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySfx(SfxClipId.CardUse);

        if (ProcessingCardBar.Instance != null)
            ProcessingCardBar.Instance.AddProcessingCard(processId, cardId);
    }


    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_RemoveProcessingCard(int processId)
    {
        if (ProcessingCardBar.Instance != null)
            ProcessingCardBar.Instance.RemoveProcessingCard(processId);
    }


    // ============================================================
    //  ライフUIを「そのクライアントのローカル視点」で更新
    // ============================================================
    public void UpdateAllLifeUIForLocal()
    {
        if (players == null) return;

        foreach (var pm in players)
        {
            if (pm != null && pm.Object != null && pm.Object.HasInputAuthority)
            {
                // このクライアントの「自分視点」UIを更新
                pm.UpdateLifeUI();
            }
        }
    }


    // プレイヤーからのドロー要求（クライアント → Host）
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestDraw(PlayerRef requester, bool resetMana)
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

        // ★ 「このドローでマナを回復するか？」フラグも渡す
        RPC_ApplyDraw(playerIndex, cardId, resetMana);
        // ▼追加：山札枚数同期（0なら引き分け）
        SyncDeckCountHostOnly();
    }

    // 効果ドロー用のリクエスト（クライアント → Host）
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_RequestEffectDraw(PlayerRef requester, int count)
    {
        if (deckManager == null) return;
        if (count <= 0) return;
        if (players == null || players.Count == 0) return;

        // requester がどの PlayerManager か特定
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
            Debug.LogWarning($"RPC_RequestEffectDraw: requester {requester} に対応する PlayerManager が見つかりません。");
            return;
        }

        // 山札から count 枚 ID を引く（Host だけ山札を触る）
        int[] ids = deckManager.DrawCardIDs(count);
        if (ids == null || ids.Length == 0)
        {
            Debug.Log("RPC_RequestEffectDraw: 山札が空、または ID を取得できませんでした。");
            return;
        }

        // 効果ドローではマナ回復はしないので resetMana = false 固定
        foreach (var id in ids)
        {
            RPC_ApplyDraw(playerIndex, id, false);
        }
        // ▼追加：山札枚数同期（0なら引き分け）
        SyncDeckCountHostOnly();
        Debug.Log($"RPC_RequestEffectDraw: playerIndex={playerIndex} に効果ドロー count={count} を適用しました。");
    }

    // 実際に手札にカードを追加するRPC（Host → 全員）
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ApplyDraw(int playerIndex, int cardId, bool resetMana)
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
        // IDからカードデータを復元して手札に追加（本人の端末だけ）
        if (pm.Object != null && pm.Object.HasInputAuthority && pm.handManager != null)
        {
            var data = deckManager.GetCardDataById(cardId);
            pm.handManager.AddCardFromData(data);
        }


        // ★SFX：ドロー（両者、1枚ごと）
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayCardMoveBurst(1);

        // ▼追加：引いたカードは「自分の端末だけ」表示
        if (pm.Object != null && pm.Object.HasInputAuthority)
        {
            if (CardMovePopupManager.Instance != null)
            {
                CardMovePopupManager.Instance.ShowDrawCards(new int[] { cardId });
            }
        }

        // ★ Host only: ドローで手札ID追加＆handCount確定
        if (Object.HasStateAuthority)
        {
            var list = GetHostHandIdList(playerIndex);
            list.Add(cardId);

            if (pm != null) pm.handCount = list.Count;
            TryCheckEx001Win(playerIndex);
        }

        // ★ 「このドローはターン開始時の選択」ならマナを回復
        if (resetMana)
        {
            pm.ResetMana();
        }

        Debug.Log($"RPC_ApplyDraw: playerIndex={playerIndex} に cardID={cardId} をドローさせました。 Reset={resetMana}");
    }



    // ============================================================
    //  効果ドロー用 共通関数（Host だけ山札を触る）
    // ============================================================
    public void EffectDraw(PlayerManager targetPlayer, int drawCount)
    {
        if (!Object.HasStateAuthority) return;
        if (deckManager == null) return;
        if (targetPlayer == null) return;
        if (drawCount <= 0) return;

        int playerIndex = players.IndexOf(targetPlayer);
        if (playerIndex < 0)
        {
            Debug.LogWarning("[EffectDraw] targetPlayer が players に見つかりません。");
            return;
        }

        int[] ids = deckManager.DrawCardIDs(drawCount);
        if (ids == null || ids.Length == 0)
        {
            Debug.Log("[EffectDraw] 山札が空です。");
            return;
        }

        // 効果ドローではマナを回復しないので resetMana = false
        foreach (var id in ids)
        {
            RPC_ApplyDraw(playerIndex, id, false);
        }
        // ▼追加：山札枚数同期（0なら引き分け）
        SyncDeckCountHostOnly();
    }

    // 現在のターンプレイヤーに効果ドローさせる簡易版
    public void EffectDrawForCurrentPlayer(int drawCount)
    {
        if (!Object.HasStateAuthority) return;
        if (deckManager == null) return;
        if (drawCount <= 0) return;

        if (currentPlayerIndex < 0 || currentPlayerIndex >= players.Count)
        {
            Debug.LogWarning("[EffectDrawForCurrentPlayer] currentPlayerIndex が不正です。");
            return;
        }

        int[] ids = deckManager.DrawCardIDs(drawCount);
        if (ids == null || ids.Length == 0)
        {
            Debug.Log("[EffectDrawForCurrentPlayer] 山札が空です。");
            return;
        }

        foreach (var id in ids)
        {
            RPC_ApplyDraw(currentPlayerIndex, id, false);
        }
        // ▼追加：山札枚数同期（0なら引き分け）
        SyncDeckCountHostOnly();
    }


    // 選択結果の提出（Client/Host → Host）
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_SubmitSealCostChoice(PlayerRef submitter, int sessionId, int cost)
    {
        if (!Object.HasStateAuthority) return;
        if (!_sealSessionActive) return;
        if (sessionId != _sealSessionId) return;
        if (cost < 1 || cost > 10) return;

        int idx = FindPlayerIndexByRef(submitter);
        if (idx < 0 || idx >= players.Count) return;

        if (idx == 0)
        {
            _sealSubmittedP0 = true;
            _sealChoiceP0 = cost;
        }
        else if (idx == 1)
        {
            _sealSubmittedP1 = true;
            _sealChoiceP1 = cost;
        }

        // SELFモード：提出した時点で確定
        if (!_sealSessionExpectBoth)
        {
            FinalizeSealSessionHost();
            return;
        }

        // BOTHモード：両方提出で確定
        if (_sealSubmittedP0 && _sealSubmittedP1)
        {
            FinalizeSealSessionHost();
        }
    }

    private void FinalizeSealSessionHost()
    {
        // このターンの封印コストに追加（重複は除外）
        if (_sealSubmittedP0 && _sealChoiceP0 >= 1 && _sealChoiceP0 <= 10 && !_sealedCostsThisTurn.Contains(_sealChoiceP0))
            _sealedCostsThisTurn.Add(_sealChoiceP0);

        if (_sealSubmittedP1 && _sealChoiceP1 >= 1 && _sealChoiceP1 <= 10 && !_sealedCostsThisTurn.Contains(_sealChoiceP1))
            _sealedCostsThisTurn.Add(_sealChoiceP1);

        // ★修正：sealed は予約語なので変数名にしない
        int[] sealedCosts = _sealedCostsThisTurn.ToArray();

        RPC_RevealSealCostChoices(
            _sealSessionId,
            _sealChoiceP0,
            _sealChoiceP1,
            _sealSubmittedP0,
            _sealSubmittedP1,
            sealedCosts
        );

        _sealSessionActive = false;
        _sealSessionExpectBoth = false;
    }

    // 確定結果を全員へ（Host → All）
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_RevealSealCostChoices(int sessionId, int cost0, int cost1, bool has0, bool has1, int[] sealedCosts)
    {
        // ローカル保持（IsCostSealedで参照）
        _sealedCostsThisTurn.Clear();
        if (sealedCosts != null) _sealedCostsThisTurn.AddRange(sealedCosts);

        _localSealResolvedSessionId = sessionId;

        // 自分の表示（常時表示）
        var localPm = GetLocalPlayerManager();
        if (localPm != null && localPm.costSealDeclareUI != null)
        {
            // 中央に「宣言：2，10」などを表示（同時宣言っぽく）
            string reveal = BuildRevealText(cost0, cost1, has0, has1);
            localPm.costSealDeclareUI.ShowReveal(reveal, sealedCosts);
        }
    }

    private string BuildRevealText(int cost0, int cost1, bool has0, bool has1)
    {
        // 両方or片方の表示を組み立て
        System.Collections.Generic.List<int> list = new System.Collections.Generic.List<int>();
        if (has0 && cost0 >= 1 && cost0 <= 10) list.Add(cost0);
        if (has1 && cost1 >= 1 && cost1 <= 10) list.Add(cost1);

        if (list.Count == 0) return "";
        string s = string.Join("，", list.Distinct().OrderBy(x => x));
        return $"使用不可：{s}";
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

        // ★追加：ターンが進んだら封印コストをクリア（そのターン中）
        _sealedCostsThisTurn.Clear();
        RPC_RevealSealCostChoices(-1, -1, -1, false, false, new int[0]);
    }


    [Rpc(sources: RpcSources.All, targets: RpcTargets.StateAuthority)]
    private void Rpc_RequestNextTurn()
    {
        NextTurn();
    }

    // プレイヤーからのチャージ要求（クライアント → Host）
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    private void RPC_RequestCharge(PlayerRef requester)
    {
        if (players.Count < 2)
            return;

        // requester がどのプレイヤーか特定
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
            Debug.LogWarning($"RPC_RequestCharge: requester {requester} に対応する PlayerManager が見つかりません。");
            return;
        }

        // ターンプレイヤー以外からのリクエストは無視
        if (playerIndex != currentPlayerIndex)
        {
            Debug.Log($"RPC_RequestCharge: ターンプレイヤーではないため無視 index={playerIndex}, current={currentPlayerIndex}");
            return;
        }

        // ★ Host から全員へ「このプレイヤーをチャージしろ」と通知
        RPC_ApplyCharge(playerIndex);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ApplyCharge(int playerIndex)
    {
        int beforeCur = 0;
        int beforeMax = 0;

        if (players != null && playerIndex >= 0 && playerIndex < players.Count && players[playerIndex] != null)
        {
            beforeCur = players[playerIndex].currentMana;
            beforeMax = players[playerIndex].maxMana;
        }

        // ★ 実際のマナ計算（Max+1 / Reset）を適用
        ApplyChargeInternal(playerIndex);

        // ★SFX：マナ増加（両者）
        if (players != null && playerIndex >= 0 && playerIndex < players.Count && players[playerIndex] != null)
        {
            int afterCur = players[playerIndex].currentMana;
            int afterMax = players[playerIndex].maxMana;

            int gainCur = afterCur - beforeCur;
            if (gainCur < 0) gainCur = 0;

            int gainMax = afterMax - beforeMax;
            if (gainMax < 0) gainMax = 0;

            int count = (gainCur > 0) ? gainCur : gainMax;

            if (count > 0 && AudioManager.Instance != null)
                AudioManager.Instance.PlaySfxBurst(SfxClipId.CardMove, count);
        }

        // ★ 各クライアントで、自分のUIから見た「相手のマナ表示」を更新
        foreach (var pm in players)
        {
            if (pm != null)
            {
                pm.UpdateEnergyUI();      // 念のため自分側も更新
                pm.UpdateOpponentUI();    // 相手側表示も更新
            }
        }
    }

    // ============================
    // マナ増加SFX（Host → 全員）
    // ============================
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_PlayCardMoveBurst(int count)
    {
        if (count <= 0) return;

        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayCardMoveBurst(count);
    }




    // ============================================================
    //  Attack / Block / Defence 同期（Host権限で進行）
    // ============================================================

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_RequestAttack(PlayerRef attackerRef, int attackCardId, int requestId)
    {
        if (battleManager == null || deckManager == null) return;
        if (players == null || players.Count < 2) return;

        int attackerIndex = FindPlayerIndexByRef(attackerRef);
        if (attackerIndex < 0) return;

        // ターン中のプレイヤー以外は拒否
        if (attackerIndex != currentPlayerIndex) return;

        int defenderIndex = (attackerIndex == 0) ? 1 : 0;

        PlayerManager attacker = players[attackerIndex];
        PlayerManager defender = players[defenderIndex];

        var attackData = deckManager.GetCardDataById(attackCardId);
        if (attackData == null) return;

        if (battleManager != null)
        {
            // ★ requestId を一緒に渡す
            battleManager.EnqueueAttack(attacker, defender, attackData, attackerRef, requestId);
        }
    }
    // ★ Attack完了通知：Host -> 全員（attacker本人だけが受け取る）
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_AttackResolvedWithSkip(PlayerRef attackerRef, int requestId, bool skipRemainingEffects)
    {
        // attacker本人の端末だけが、このrequestIdを完了として受け取ればOK
        if (runner == null) runner = FindAnyObjectByType<NetworkRunner>();
        if (runner == null) return;

        if (runner.LocalPlayer != attackerRef) return;

        CardGenerator.NotifyAttackResolved(requestId, skipRemainingEffects);
    }

    // 既存互換（skipなし）
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_AttackResolved(PlayerRef attackerRef, int requestId)
    {
        if (runner == null) runner = FindAnyObjectByType<NetworkRunner>();
        if (runner == null) return;

        if (runner.LocalPlayer != attackerRef) return;

        CardGenerator.NotifyAttackResolved(requestId, false);
    }

    // ============================================================
    // ターン終了系（効果発動による：自分/相手/どちらのターンでも）
    // mode: 0=any / 1=my turn only / 2=opponent turn only
    // ============================================================

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_RequestEndTurnByEffect(PlayerRef requester, int mode, bool skipOpponentCardRemainingEffects)
    {
        TryEndTurnByEffectHost(requester, mode, skipOpponentCardRemainingEffects);
    }

    // ★Host専用：判定してターンを終了する
    public void TryEndTurnByEffectHost(PlayerRef requester, int mode, bool skipOpponentCardRemainingEffects)
    {
        if (_isGameOver) return;
        if (players == null || players.Count < 2) return;
        if (Object == null || !Object.HasStateAuthority) return;

        int requesterIndex = FindPlayerIndexByRef(requester);
        if (requesterIndex < 0) return;

        bool shouldEnd = false;
        switch (mode)
        {
            case 0: shouldEnd = true; break;                            // どちらのターンでも終了
            case 1: shouldEnd = (requesterIndex == currentPlayerIndex); break; // 自分のターンなら終了
            case 2: shouldEnd = (requesterIndex != currentPlayerIndex); break; // 相手のターンなら終了
            default: shouldEnd = true; break;
        }

        if (!shouldEnd) return;

        // ★攻撃処理中なら、攻撃カードの残り効果スキップを予約
        if (skipOpponentCardRemainingEffects && battleManager != null)
        {
            battleManager.MarkSkipRemainingEffectsForCurrentAttack(requester);
        }

        if (turnChoicePanel != null)
            turnChoicePanel.SetActive(false);

        if (endTurnButton != null)
            endTurnButton.interactable = false;

        NextTurn();
    }


    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_OpenBlockChoice(PlayerRef defenderRef)
    {
        // この端末が defender じゃなければ閉じるだけ
        var r = runner;
        if (r == null) r = FindAnyObjectByType<NetworkRunner>();
        if (r == null) return;

        if (r.LocalPlayer != defenderRef)
        {
            if (BlockWindow.Instance != null) BlockWindow.Instance.gameObject.SetActive(false);
            return;
        }

        // defender端末だけ、ローカルの players[] から index を引き直す
        int defenderIndex = FindPlayerIndexByRef(defenderRef);
        if (defenderIndex < 0) return;

        StartCoroutine(Co_BlockChoice(defenderIndex));
    }

    private IEnumerator Co_BlockChoice(int defenderIndex)
    {
        if (BlockWindow.Instance == null) yield break;
        if (players == null || defenderIndex < 0 || defenderIndex >= players.Count) yield break;

        var defender = players[defenderIndex];

        yield return StartCoroutine(BlockWindow.Instance.ShowBlockChoice(defender));

        int chosenId = -1;
        var data = BlockWindow.Instance.GetSelectedBlockData();
        if (data != null) chosenId = data.id;

        var r = runner;
        if (r == null) r = FindAnyObjectByType<NetworkRunner>();

        if (r != null)
        {
            RPC_SubmitBlockChoice(r.LocalPlayer, chosenId);
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_SubmitBlockChoice(PlayerRef defenderRef, int chosenCardIdOrMinusOne)
    {
        if (battleManager == null) return;
        battleManager.ReceiveBlockChoice(defenderRef, chosenCardIdOrMinusOne);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_OpenDefenceChoice(PlayerRef defenderRef, int destroyedLifeCardId)
    {
        var r = runner;
        if (r == null) r = FindAnyObjectByType<NetworkRunner>();
        if (r == null) return;

        if (r.LocalPlayer != defenderRef)
        {
            if (DefenceWindow.Instance != null) DefenceWindow.Instance.gameObject.SetActive(false);
            return;
        }

        int defenderIndex = FindPlayerIndexByRef(defenderRef);
        if (defenderIndex < 0) return;

        StartCoroutine(Co_DefenceChoice(defenderIndex, destroyedLifeCardId));
    }


    private IEnumerator Co_DefenceChoice(int defenderIndex, int destroyedLifeCardId)
    {
        if (DefenceWindow.Instance == null) yield break;
        if (deckManager == null) yield break;
        if (players == null || defenderIndex < 0 || defenderIndex >= players.Count) yield break;

        var defender = players[defenderIndex];
        var data = deckManager.GetCardDataById(destroyedLifeCardId);
        if (data == null) yield break;

        yield return StartCoroutine(DefenceWindow.Instance.ShowDefenceChoice(defender, data));

        bool used = DefenceWindow.Instance.GetUseDefenceResult();

        var r = runner;
        if (r == null)
            r = FindAnyObjectByType<NetworkRunner>();

        if (r != null)
        {
            RPC_NotifyDefenceChoiceDone(r.LocalPlayer, used);
        }
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_NotifyDefenceChoiceDone(PlayerRef defenderRef, bool usedDefence)
    {
        if (battleManager == null) return;
        battleManager.ReceiveDefenceChoiceDone(defenderRef, usedDefence);
    }

    private int FindPlayerIndexByRef(PlayerRef r)
    {
        if (players == null) return -1;

        for (int i = 0; i < players.Count; i++)
        {
            var pm = players[i];
            if (pm != null && pm.Object != null && pm.Object.InputAuthority == r)
                return i;
        }
        return -1;
    }

    // ============================
    // マナ関連 共通UI更新（Host で値を変えた後に呼ぶ）
    // ============================
    private void UpdateAllManaUI()
    {
        if (players == null) return;

        foreach (var pm in players)
        {
            if (pm != null)
            {
                pm.UpdateEnergyUI();
                pm.UpdateOpponentUI();
            }
        }
    }

    // ============================
    // マナ効果（Host中心）
    // ============================

    // Host 側のみ: 特定プレイヤーの最大マナを増やす（カード効果用）
    public void EffectManaBoost(PlayerManager targetPlayer, int amount)
    {
        if (!Object.HasStateAuthority) return;
        if (targetPlayer == null) return;
        if (amount <= 0) return;

        int beforeMax = targetPlayer.maxMana;

        targetPlayer.IncreaseMaxManaOnly(amount);

        int maxUp = targetPlayer.maxMana - beforeMax;
        if (maxUp < 0) maxUp = 0;

        // UIは全員側で更新＆音も全員に鳴らす
        RPC_RefreshManaUIAndSfx(maxUp, 0, 0);
    }


    public void EffectManaReduce(PlayerManager targetPlayer, int amount)
    {
        if (!Object.HasStateAuthority) return;
        if (targetPlayer == null) return;
        if (amount <= 0) return;

        int beforeMax = targetPlayer.maxMana;

        targetPlayer.DecreaseMaxManaOnly(amount);

        int maxDown = beforeMax - targetPlayer.maxMana;
        if (maxDown < 0) maxDown = 0;

        RPC_RefreshManaUIAndSfx(0, maxDown, 0);
    }


    // 効果 ManaReduceSelf 用リクエスト（Client → Host）
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_RequestEffectManaReduceSelf(PlayerRef requester, int amount)
    {
        if (amount <= 0) return;
        if (players == null || players.Count == 0) return;

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

        if (playerIndex < 0) return;

        EffectManaReduce(players[playerIndex], amount);
    }

    // 効果 ManaReduceOpponent 用リクエスト（Client → Host）
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_RequestEffectManaReduceOpponent(PlayerRef requester, int amount)
    {
        if (amount <= 0) return;
        if (players == null || players.Count < 2) return;

        int attackerIndex = -1;
        for (int i = 0; i < players.Count; i++)
        {
            var pm = players[i];
            if (pm != null && pm.Object != null && pm.Object.InputAuthority == requester)
            {
                attackerIndex = i;
                break;
            }
        }

        if (attackerIndex < 0) return;

        int defenderIndex = (attackerIndex == 0) ? 1 : 0;

        EffectManaReduce(players[defenderIndex], amount);
    }

    // 効果 ManaReduceIfMyTurn 用リクエスト（Client → Host）
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_RequestEffectManaReduceIfMyTurn(PlayerRef requester, int amount)
    {
        if (amount <= 0) return;
        if (players == null || players.Count == 0) return;

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

        if (playerIndex < 0) return;

        // 自分のターンじゃないなら何もしない
        if (playerIndex != currentPlayerIndex) return;

        EffectManaReduce(players[playerIndex], amount);
    }

    // Host 側のみ: 特定プレイヤーのマナ回復。isAll=true の場合は全回復。
    public void EffectManaRecover(PlayerManager targetPlayer, int amount, bool isAll)
    {
        if (!Object.HasStateAuthority) return;
        if (targetPlayer == null) return;
        if (!isAll && amount <= 0) return;

        int beforeCur = targetPlayer.currentMana;

        if (isAll)
        {
            targetPlayer.ResetMana();
        }
        else
        {
            targetPlayer.GainMana(amount);
        }

        int recover = targetPlayer.currentMana - beforeCur;
        if (recover < 0) recover = 0;

        RPC_RefreshManaUIAndSfx(0, 0, recover);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_RefreshManaUIAndSfx(int maxUp, int maxDown, int recover)
    {
        // 各クライアントでUI更新
        UpdateAllManaUI();

        var am = AudioManager.Instance;
        if (am == null) return;

        if (maxUp > 0) am.PlayManaMaxUpBurst(maxUp);
        if (maxDown > 0) am.PlayManaMaxDownBurst(maxDown);
        if (recover > 0) am.PlayManaRecoverBurst(recover);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_PlaySharedSfx(int sfxId)
    {
        var am = AudioManager.Instance;
        if (am == null) return;

        am.PlaySfx((SfxClipId)sfxId);
    }



    // 効果 ManaBoost 用リクエスト（Client → Host）
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_RequestEffectManaBoost(PlayerRef requester, int amount)
    {
        if (amount <= 0) return;
        if (players == null || players.Count == 0) return;

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

        if (playerIndex < 0) return;

        EffectManaBoost(players[playerIndex], amount);
    }

    // 効果 ManaRecover 用リクエスト（Client → Host）
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_RequestEffectManaRecover(PlayerRef requester, int amount, bool isAll)
    {
        if (!isAll && amount <= 0) return;
        if (players == null || players.Count == 0) return;

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

        if (playerIndex < 0) return;

        EffectManaRecover(players[playerIndex], amount, isAll);
    }


    // ============================
    // DEFENCE効果専用（Host中心）
    // 「ライフが0ならライフを1追加」を Host が判定して実行
    // ============================
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_RequestEffectDefenceLifeIfZero(PlayerRef targetRef)
    {
        if (!Object.HasStateAuthority) return;

        int idx = FindPlayerIndexByRef(targetRef);
        if (idx < 0 || players == null || idx >= players.Count) return;

        var targetPlayer = players[idx];
        if (targetPlayer == null || targetPlayer.lifeManager == null) return;

        int lifeCount = GetLifeCountSafe(targetPlayer);
        if (lifeCount <= 0)
        {
            // Host 側で山札からライフカードを引き → 全員に同期
            AddLifeToPlayer(targetPlayer, 1);
        }
    }

    // ★ LifeManager の実装差異に強い「現在ライフ枚数」取得（Host判定用）
    private int GetLifeCountSafe(PlayerManager pm)
    {
        if (pm == null || pm.lifeManager == null) return 0;

        object lm = pm.lifeManager;
        var t = lm.GetType();

        var p1 = t.GetProperty("LifeCount");
        if (p1 != null)
        {
            try { return (int)p1.GetValue(lm); } catch { }
        }

        var p2 = t.GetProperty("lifeCount");
        if (p2 != null)
        {
            try { return (int)p2.GetValue(lm); } catch { }
        }

        foreach (var fname in new[] { "lifeCards", "lifeList", "cards", "life" })
        {
            var f = t.GetField(fname);
            if (f != null)
            {
                try
                {
                    var v = f.GetValue(lm);
                    if (v is System.Collections.ICollection col) return col.Count;
                }
                catch { }
            }
        }

        foreach (var pname in new[] { "Count", "Length" })
        {
            var p = t.GetProperty(pname);
            if (p != null)
            {
                try
                {
                    var v = p.GetValue(lm);
                    if (v is int i) return i;
                }
                catch { }
            }
        }

        return 0;
    }


    // ★★★ 追加：カードのコスト支払い用（Client → Host） ★★★
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_RequestSpendMana(PlayerRef requester, int cost)
    {
        if (cost <= 0) return;
        if (players == null || players.Count == 0) return;

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

        if (playerIndex < 0) return;

        var target = players[playerIndex];
        if (target == null) return;

        if (target.currentMana < cost)
        {
            Debug.Log($"[Host] RPC_RequestSpendMana: マナ不足 cost={cost}, current={target.currentMana}");
            return;
        }

        int beforeCur = target.currentMana;

        target.currentMana -= cost;

        // ★マナ減少SFX（両者）
        int dec = beforeCur - target.currentMana;
        if (dec > 0)
            RPC_PlayCardMoveBurst(dec);

        UpdateAllManaUI();


        UpdateAllManaUI();
    }

    // ============================================================
    // Discard 同期（捨て札共通化）
    // ============================================================

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_RequestAddDiscard(PlayerRef requester, int cardId)
    {
        if (!Object.HasStateAuthority) return;

        // ★ Host only: requester の手札IDリストからこのIDを1枚だけ抜く
        int pIndex = FindPlayerIndexByRef(requester);
        if (pIndex >= 0)
        {
            var list = GetHostHandIdList(pIndex);
            list.Remove(cardId); // 同IDが複数あれば1枚だけ消える

            if (players[pIndex] != null) players[pIndex].handCount = list.Count;
        }


        // 最小構成：Hostが確定して全員へ同期
        RPC_SyncAddDiscard(cardId);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_SyncAddDiscard(int cardId)
    {
        if (deckManager == null) deckManager = FindAnyObjectByType<DeckManager>();
        if (discardManager == null) discardManager = FindAnyObjectByType<DiscardManager>();
        if (deckManager == null || discardManager == null) return;

        var data = deckManager.GetCardDataById(cardId);
        if (data != null)
        {
            discardManager.AddToDiscard(data);
        }
    }

    // ============================================================
    //  ライフ→手札（Defenceで使わなかった時など）を Host 経由で同期
    // ============================================================
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_RequestAddHandFromLife(PlayerRef requester, int cardId)
    {
        if (!Object.HasStateAuthority) return;
        if (_isGameOver) return;
        if (cardId <= 0) return;

        int playerIndex = FindPlayerIndexByRef(requester);
        if (playerIndex < 0 || playerIndex >= players.Count) return;

        var list = GetHostHandIdList(playerIndex);
        list.Add(cardId);

        var pm = players[playerIndex];
        if (pm != null) pm.handCount = list.Count;

        RPC_ApplyAddHandFromLife(playerIndex, cardId);

        // ▼EX勝利（ライフ→手札に入った瞬間）
        TryCheckEx001Win(playerIndex);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ApplyAddHandFromLife(int playerIndex, int cardId)
    {
        if (deckManager == null) deckManager = FindAnyObjectByType<DeckManager>();
        if (playerIndex < 0 || playerIndex >= players.Count) return;

        var pm = players[playerIndex];
        if (pm == null || pm.handManager == null) return;

        // 手札の実体は「本人のクライアントだけ」追加
        if (pm.Object != null && pm.Object.HasInputAuthority)
        {
            var data = deckManager.GetCardDataById(cardId);
            pm.handManager.AddCardFromData(data);
        }

        // handCount表示など（あなたの既存同期設計に合わせて）
        if (pm != null)
            pm.NotifyHandChangedForBothSides();
    }


    // ============================================================
    //  捨て札回収（RecoverDiscard）同期
    // ============================================================

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_RequestRecoverDiscard(PlayerRef requester, int sessionId, int[] recoverIds)
    {
        if (!Object.HasStateAuthority) return;
        if (recoverIds == null) recoverIds = new int[0];

        if (deckManager == null) deckManager = FindAnyObjectByType<DeckManager>();
        if (discardManager == null) discardManager = FindAnyObjectByType<DiscardManager>();
        if (deckManager == null || discardManager == null)
        {
            // ★失敗でも「完了通知」は返す（待ちが止まらないように）
            RPC_SyncRecoverDiscard(-1, sessionId, requester, new int[0]);
            return;
        }

        int playerIndex = FindPlayerIndexByRef(requester);
        if (playerIndex < 0 || playerIndex >= players.Count)
        {
            RPC_SyncRecoverDiscard(-1, sessionId, requester, new int[0]);
            return;
        }

        // Hostが捨て札から実在する分だけ確定して回収させる（※ここでは消さない）
        List<int> accepted = new List<int>();

        // idごとの残数を作る（重複ID対応）
        Dictionary<int, int> remain = new Dictionary<int, int>();
        foreach (var d in discardManager.discardDataList)
        {
            if (d == null) continue;
            if (remain.TryGetValue(d.id, out int c)) remain[d.id] = c + 1;
            else remain[d.id] = 1;
        }

        foreach (int id in recoverIds)
        {
            if (remain.TryGetValue(id, out int c) && c > 0)
            {
                accepted.Add(id);
                remain[id] = c - 1; // ★このIDを1枚分確保
            }
        }

#if UNITY_EDITOR
        Debug.Log($"[RecoverDiscard][Host] req={string.Join(",", recoverIds)} accepted={string.Join(",", accepted)} discardTotal={discardManager.discardDataList.Count}");
#endif

        // accepted が空でも「完了通知」は返す（待ちが止まらないように）
        if (accepted.Count > 0)
        {
            // Hostの手札IDリストにも反映（EX判定のため）
            var list = GetHostHandIdList(playerIndex);
            foreach (var id in accepted) list.Add(id);

            var pm = players[playerIndex];
            if (pm != null) pm.handCount = list.Count;

            TryCheckEx001Win(playerIndex);
        }

        // ★成功/失敗どちらでも返す（成功は accepted、失敗は空配列）
        RPC_SyncRecoverDiscard(playerIndex, sessionId, requester, accepted.ToArray());
    }


    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_SyncRecoverDiscard(int playerIndex, int sessionId, PlayerRef requester, int[] recoverIds)
    {
        // ★最優先：待機解除（途中returnしても止まらない）
        var r = runner;
        if (r == null) r = FindAnyObjectByType<NetworkRunner>();
        if (r != null && r.LocalPlayer == requester)
            _localRecoverResolvedSessionId = sessionId;

        if (recoverIds == null || recoverIds.Length == 0) return;

        if (deckManager == null) deckManager = FindAnyObjectByType<DeckManager>();
        if (discardManager == null) discardManager = FindAnyObjectByType<DiscardManager>();

        if (deckManager == null)
        {
            var all = Resources.FindObjectsOfTypeAll<DeckManager>();
            if (all != null && all.Length > 0) deckManager = all[0];
        }
        if (discardManager == null)
        {
            var all = Resources.FindObjectsOfTypeAll<DiscardManager>();
            if (all != null && all.Length > 0) discardManager = all[0];
        }

        if (deckManager == null || discardManager == null) return;

        // まず捨て札から削除（全員同じ結果に）
        foreach (int id in recoverIds)
            discardManager.RemoveFromDiscardById(id);

        // 手札の実体追加は「本人だけ」
        if (players == null || playerIndex < 0 || playerIndex >= players.Count) return;

        var pm = players[playerIndex];
        if (pm != null && pm.Object != null && pm.Object.HasInputAuthority && pm.handManager != null)
        {
            foreach (int id in recoverIds)
            {
                var data = deckManager.GetCardDataById(id);
                if (data != null) pm.handManager.AddCardFromData(data);
            }

            pm.handManager.UpdateCardPositions();
            pm.UpdateHandCountUI();
        }

        if (CardMovePopupManager.Instance != null)
            CardMovePopupManager.Instance.ShowRecoverCards(recoverIds);
    }






    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_RequestDiscardFromHand(PlayerRef requester, PlayerRef targetRef, int[] cardIds)
    {
        if (!Object.HasStateAuthority) return;
        if (cardIds == null || cardIds.Length == 0) return;

        int targetIndex = FindPlayerIndexByRef(targetRef);
        if (targetIndex < 0) return;

        var list = GetHostHandIdList(targetIndex);

        // Host確定：手札にある分だけ採用（多重IDにも対応）
        List<int> accepted = new List<int>();
        foreach (int id in cardIds)
        {
            if (list.Contains(id))
            {
                list.Remove(id);
                accepted.Add(id);
            }
        }

        if (accepted.Count == 0) return;

        // handCount 確定
        if (players[targetIndex] != null) players[targetIndex].handCount = list.Count;

        // 全員に適用
        RPC_SyncDiscardFromHand(targetIndex, accepted.ToArray());
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_SyncDiscardFromHand(int targetIndex, int[] cardIds)
    {
        if (cardIds == null || cardIds.Length == 0) return;
        if (players == null || targetIndex < 0 || targetIndex >= players.Count) return;

        if (deckManager == null) deckManager = FindAnyObjectByType<DeckManager>();
        if (discardManager == null) discardManager = FindAnyObjectByType<DiscardManager>();

        var targetPM = players[targetIndex];
        if (targetPM == null) return;

        // ★SFX：手札から捨てる（両者）
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySfxBurst(SfxClipId.CardMove, cardIds.Length);

        // 1) 共有捨て札へ追加（全員で同じ結果）
        if (deckManager != null && discardManager != null)
        {
            foreach (int id in cardIds)
            {
                var data = deckManager.GetCardDataById(id);
                if (data != null) discardManager.AddToDiscard(data);
            }
        }

        // ▼追加：捨てたカードは両者に表示（大量になり得るのでグリッド＆同IDは枚数表示でまとめる）
        if (CardMovePopupManager.Instance != null)
        {
            CardMovePopupManager.Instance.ShowDiscardCards(cardIds);
        }

        // 2) 対象本人だけ：手札の実カード(GameObject)を消す
        if (targetPM.Object != null && targetPM.Object.HasInputAuthority && targetPM.handManager != null)
        {
            foreach (int id in cardIds)
            {
                RemoveLocalHandCardById(targetPM, id);
            }

            targetPM.handManager.UpdateCardPositions();
            targetPM.UpdateHandCountUI(); // 自分画面の即時反映
        }
    }


    // ============================================================
    //  手札の入れ替え（Host確定）
    //  自分と相手の手札を丸ごと交換する
    // ============================================================

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_RequestSwapHands(PlayerRef requester)
    {
        if (!Object.HasStateAuthority) return;
        if (_isGameOver) return;
        if (players == null || players.Count < 2) return;

        // Hostの手札IDリストを丸ごとスワップ
        var tmp = hostHandIdsP0;
        hostHandIdsP0 = hostHandIdsP1;
        hostHandIdsP1 = tmp;

        // handCount を確定（相手の裏面枚数表示に使われる）
        if (players[0] != null) players[0].handCount = hostHandIdsP0.Count;
        if (players[1] != null) players[1].handCount = hostHandIdsP1.Count;

        // 全員に適用（各端末で “自分の手札実体” を作り直す）
        RPC_ApplySwapHands(hostHandIdsP0.ToArray(), hostHandIdsP1.ToArray());

        // EX勝利が成立してたら即チェック（手札が入れ替わった瞬間に揃うケースがある）
        TryCheckEx001Win(0);
        TryCheckEx001Win(1);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_ApplySwapHands(int[] p0Hand, int[] p1Hand)
    {
        if (players == null || players.Count < 2) return;

        if (deckManager == null) deckManager = FindAnyObjectByType<DeckManager>();
        if (deckManager == null)
        {
            Debug.LogError("RPC_ApplySwapHands: deckManager が見つかりません。");
            return;
        }

        // “自分” の PlayerManager を探す（InputAuthority のみ手札実体を作る）
        int localIndex = -1;
        for (int i = 0; i < players.Count; i++)
        {
            if (players[i] != null && players[i].Object != null && players[i].Object.HasInputAuthority)
            {
                localIndex = i;
                break;
            }
        }
        if (localIndex < 0) return;

        var localPM = players[localIndex];
        if (localPM == null || localPM.handManager == null) return;

        int[] myHand = (localIndex == 0) ? p0Hand : p1Hand;

        RebuildLocalHand(localPM, myHand);
    }

    // 対象本人の端末だけで実行される「手札実体の総入れ替え」
    private void RebuildLocalHand(PlayerManager pm, int[] newHandIds)
    {
        if (pm == null || pm.handManager == null) return;

        // 1) いまの手札実体を全削除（handCards + 念のための取りこぼしも）
        var toDestroy = new HashSet<GameObject>();

        foreach (var go in pm.handManager.handCards)
        {
            if (go != null) toDestroy.Add(go);
        }

        // 念のため：手札リストから外れてるカード（ドラッグ中など）も消す
        var cgs = pm.handManager.GetComponentsInChildren<CardGenerator>(true);
        foreach (var cg in cgs)
        {
            if (cg != null && cg.cardData != null && cg.gameObject != null)
                toDestroy.Add(cg.gameObject);
        }

        foreach (var go in toDestroy)
        {
            if (go != null) Destroy(go);
        }

        pm.handManager.handCards.Clear();

        // 2) 新しいID配列で手札を再生成
        if (newHandIds != null)
        {
            foreach (var id in newHandIds)
            {
                var data = deckManager.GetCardDataById(id);
                if (data != null)
                {
                    pm.handManager.AddCardFromData(data);
                }
            }
        }

        // 3) 並べ直し＆自分画面のUI更新
        pm.handManager.UpdateCardPositions();
        pm.UpdateHandCountUI();
    }



    // 対象本人の端末だけで実行される「手札実体の削除」
    private void RemoveLocalHandCardById(PlayerManager pm, int cardId)
    {
        if (pm == null || pm.handManager == null) return;

        GameObject found = null;

        // handCards を優先検索
        foreach (var go in pm.handManager.handCards)
        {
            if (go == null) continue;
            var cg = go.GetComponent<CardGenerator>();
            if (cg != null && cg.cardData != null && cg.cardData.id == cardId)
            {
                found = go;
                break;
            }
        }

        // 念のため：手札リストから外れてるカードも探す（ドラッグ中等）
        if (found == null)
        {
            var cgs = pm.handManager.GetComponentsInChildren<CardGenerator>(true);
            foreach (var cg in cgs)
            {
                if (cg != null && cg.cardData != null && cg.cardData.id == cardId)
                {
                    found = cg.gameObject;
                    break;
                }
            }
        }

        if (found != null)
        {
            pm.handManager.handCards.Remove(found);
            Destroy(found);
        }
    }

    // ============================================================
    //  ランダム捨て（Host確定）
    // ============================================================

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_RequestRandomDiscard(PlayerRef requester, PlayerRef targetRef, int count)
    {
        if (!Object.HasStateAuthority) return;

        int targetIndex = FindPlayerIndexByRef(targetRef);
        if (targetIndex < 0 || targetIndex >= players.Count) return;

        if (count <= 0) return;

        var list = GetHostHandIdList(targetIndex);
        if (list == null || list.Count == 0) return;

        int n = Mathf.Min(count, list.Count);

        List<int> accepted = new List<int>(n);

        // Hostがランダムに選んで確定（ここだけで完結させる）
        for (int i = 0; i < n; i++)
        {
            int idx = UnityEngine.Random.Range(0, list.Count);
            int id = list[idx];
            list.RemoveAt(idx);
            accepted.Add(id);
        }

        // handCount確定
        if (players[targetIndex] != null)
            players[targetIndex].handCount = list.Count;

        // 全員へ適用（捨て札追加＋対象本人の手札実体削除）
        RPC_SyncDiscardFromHand(targetIndex, accepted.ToArray());
    }
    // ============================================================
    //  Deck UI / GameOver / EX Win
    // ============================================================

    private void UpdateDeckCountUI(int remaining)
    {
        if (deckCountText != null)
            deckCountText.text = $"DECK: {remaining}";
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_SyncDeckCount(int remaining)
    {
        UpdateDeckCountUI(remaining);
    }

    // Host専用：山札枚数を全員に同期＋0なら引き分け
    private void SyncDeckCountHostOnly()
    {
        if (!Object.HasStateAuthority) return;

        if (deckManager == null) deckManager = FindAnyObjectByType<DeckManager>();

        int remain = (deckManager != null) ? deckManager.GetRemainingCount() : 0;
        RPC_SyncDeckCount(remain);

        // 「最後のカードを引いた時」＝引いた後に0になってたら引き分け
        if (remain <= 0)
        {
            TryEndGameDraw("山札切れ");
        }
    }

    private void EndGameInternal(int winnerIndex, string reason)
    {
        if (_isGameOver) return;
        _isGameOver = true;

        RPC_GameOver(winnerIndex, reason);
    }
    private void EndGameEx001Internal(int winnerIndex, string exImageName)
    {
        if (_isGameOver) return;
        _isGameOver = true;

        if (string.IsNullOrEmpty(exImageName))
            exImageName = EX_CARD_NAME; // 保険（画像名がEX_001と同じ前提ならこれで出る）

        RPC_Ex001WinSequence(winnerIndex, "EX_001が5枚揃った", exImageName);
    }

    private void TryEndGameDraw(string reason)
    {
        if (_isGameOver) return;
        EndGameInternal(-1, reason);
    }

    // Attack終了時のライフ0チェック（EX勝利が先に出てたらここでは何もしない）
    public bool TryEndGameByLifeZeroAfterAttack(PlayerManager attacker, PlayerManager defender)
    {
        if (!Object.HasStateAuthority) return false;
        if (_isGameOver) return false;
        if (attacker == null || defender == null) return false;

        int defenderLife = GetLifeCountSafe(defender);
        if (defenderLife > 0) return false;

        int attackerIndex = players.IndexOf(attacker);
        if (attackerIndex < 0) attackerIndex = 0;

        EndGameInternal(attackerIndex, "Attack終了時に相手ライフが0");
        return true;
    }

    // EX_001：手札に5枚揃った瞬間勝利（Host手札IDリストで判定）
    private bool TryCheckEx001Win(int playerIndex)
    {
        if (!Object.HasStateAuthority) return false;
        if (_isGameOver) return false;
        if (deckManager == null) deckManager = FindAnyObjectByType<DeckManager>();

        var list = GetHostHandIdList(playerIndex);
        if (list == null) return false;

        int exCount = 0;
        string exImageName = null;

        foreach (var id in list)
        {
            var data = deckManager.GetCardDataById(id);
            if (data != null && data.name == EX_CARD_NAME)
            {
                exCount++;
                if (string.IsNullOrEmpty(exImageName))
                    exImageName = data.image; // ★EX画像名（Resources/CardImages/ から読む用）
            }
        }

        if (exCount >= EX_WIN_COUNT)
        {
            EndGameEx001Internal(playerIndex, exImageName);
            return true;
        }

        return false;
    }
    private void ApplyGameOverCommonSetup()
    {
        // 以降の操作を止める
        _lockHandCardUse = true;

        if (turnChoicePanel != null)
            turnChoicePanel.SetActive(false);

        if (blockWindow != null) blockWindow.gameObject.SetActive(false);
        if (defenceWindow != null) defenceWindow.gameObject.SetActive(false);
    }
    private void ShowGameOverPanelLocal(int winnerIndex, string reason)
    {
        // 自分が勝ちか負けか判定
        var r = runner;
        if (r == null) r = FindAnyObjectByType<NetworkRunner>();

        int myIndex = -1;
        if (r != null)
            myIndex = FindPlayerIndexByRef(r.LocalPlayer);

        string resultText;
        if (winnerIndex < 0)
            resultText = "DRAW";
        else if (myIndex >= 0 && myIndex == winnerIndex)
            resultText = "WIN";
        else
            resultText = "LOSE";

        // ★BGM切替＆勝敗SE
        var am = AudioManager.Instance;
        if (am != null && am.library != null)
        {
            if (am.library.bgmResult != null)
                am.ChangeBgm(am.library.bgmResult, loop: true, volumeScale: 1f);

            if (resultText == "WIN")
                am.PlaySfx(SfxClipId.Victory);
            else if (resultText == "LOSE")
                am.PlaySfx(SfxClipId.Defeat);
        }

        string finalReason = reason;

        // ★リタイア理由だけは勝者/敗者で文言を変える
        if (reason == REASON_KEY_RETIRE)
        {
            if (winnerIndex >= 0 && myIndex >= 0 && myIndex == winnerIndex)
                finalReason = "相手がリタイアした";
            else
                finalReason = "リタイアした";
        }

        // UI表示（GameOverPanelがあればそっち優先）
        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);

            if (gameOverResultText != null)
                gameOverResultText.text = resultText;

            if (gameOverReasonText != null)
                gameOverReasonText.text = finalReason;
        }
        else
        {
            // フォールバック（EffectWindow）
            string msg = (winnerIndex < 0)
                ? $"引き分け：{finalReason}"
                : $"勝利：P{winnerIndex + 1}（{finalReason}）";

            if (effectWindow != null)
                StartCoroutine(effectWindow.ShowProcessAuto(msg, 2.0f, false));
            else
                Debug.Log(msg);
        }
    }


    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_GameOver(int winnerIndex, string reason)
    {
        _isGameOver = true;

        ApplyGameOverCommonSetup();
        ShowGameOverPanelLocal(winnerIndex, reason);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_Ex001WinSequence(int winnerIndex, string reason, string exImageName)
    {
        _isGameOver = true;

        ApplyGameOverCommonSetup();

        // すでに勝敗パネルが出てたら一旦閉じる（念のため）
        if (gameOverPanel != null) gameOverPanel.SetActive(false);

        StartCoroutine(Co_Ex001WinSequence(winnerIndex, reason, exImageName));
    }

    private IEnumerator Co_Ex001WinSequence(int winnerIndex, string reason, string exImageName)
    {
        // 負け側：相手手札の裏面をEX画像にフリップ
        var r = runner;
        if (r == null) r = FindAnyObjectByType<NetworkRunner>();

        int myIndex = -1;
        if (r != null)
            myIndex = FindPlayerIndexByRef(r.LocalPlayer);

        bool iAmLoser = (winnerIndex >= 0 && myIndex >= 0 && myIndex != winnerIndex);

        if (iAmLoser)
        {
            // 自分の PlayerManager の「相手手札裏面」を表にする
            if (players != null && myIndex >= 0 && myIndex < players.Count && players[myIndex] != null)
            {
                yield return StartCoroutine(players[myIndex].Co_RevealOpponentHandAsEx(
                    exImageName,
                    EX_WIN_COUNT,
                    ex001FlipDuration,
                    ex001FlipInterval
                ));
            }
        }

        // 両者共通：5枚を左→右へフェード表示
        yield return StartCoroutine(Co_PlayEx001Cutin(exImageName));

        // クリック or 10秒待機
        float elapsed = 0f;
        while (elapsed < ex001WaitSeconds)
        {
            if (Input.GetMouseButtonDown(0) || Input.touchCount > 0)
                break;

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (ex001CutinPanel != null)
            ex001CutinPanel.SetActive(false);

        // 最後にいつもの勝敗UI
        ShowGameOverPanelLocal(winnerIndex, reason);
    }

    private IEnumerator Co_PlayEx001Cutin(string exImageName)
    {
        if (ex001CutinPanel == null || ex001CutinImages == null || ex001CutinImages.Length < EX_WIN_COUNT)
            yield break;

        var sprite = LoadEx001Sprite(exImageName);

        if (sprite == null)
        {
            Debug.LogWarning($"EX sprite not found: CardImages/{exImageName}");
            yield break;
        }

        ex001CutinPanel.SetActive(true);

        for (int i = 0; i < EX_WIN_COUNT; i++)
        {
            var img = ex001CutinImages[i];
            if (img == null) continue;

            img.sprite = sprite;

            var c = img.color;
            c.a = 0f;
            img.color = c;
        }

        for (int i = 0; i < EX_WIN_COUNT; i++)
        {
            var img = ex001CutinImages[i];
            if (img == null) continue;

            StartCoroutine(Co_FadeImage(img, 0f, 1f, ex001FadeDuration));
            yield return new WaitForSecondsRealtime(ex001StepInterval);
        }

        yield return new WaitForSecondsRealtime(ex001FadeDuration);
    }

    private Sprite LoadEx001Sprite(string exImageName)
    {
        // exImageName が "4001" でも "card4001" でもOKにする
        string spriteName = exImageName;
        if (!spriteName.StartsWith("card"))
            spriteName = spriteName+"card";

        // 目的のパス（最優先）
        var sp = Resources.Load<Sprite>("cardPNG/" + spriteName);
        if (sp != null) return sp;

        // 念のためのフォールバック（既存環境を壊さない）
        sp = Resources.Load<Sprite>("CardImages/" + exImageName);
        if (sp != null) return sp;

        sp = Resources.Load<Sprite>("CardImage/" + exImageName);
        return sp;
    }


    private IEnumerator Co_FadeImage(Image img, float from, float to, float duration)
    {
        if (img == null) yield break;

        float t = 0f;
        Color baseC = img.color;

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float a = Mathf.Lerp(from, to, Mathf.Clamp01(t / duration));
            img.color = new Color(baseC.r, baseC.g, baseC.b, a);
            yield return null;
        }

        img.color = new Color(baseC.r, baseC.g, baseC.b, to);
    }



    public void OnReturnHomeClicked()
    {
        StartCoroutine(Co_ReturnHome());
    }

    private IEnumerator Co_ReturnHome()
    {
        // 念のため入力ロック
        _lockHandCardUse = true;

        var r = runner;
        if (r == null) r = FindAnyObjectByType<NetworkRunner>();

        // まだ動いてたら切断
        if (r != null && r.IsRunning)
        {
            r.Shutdown();
            // ちょい待ち（完全終了待ちの保険）
            yield return new WaitForSeconds(0.2f);
        }

        SceneManager.LoadScene(homeSceneName);
    }


    // 宣言セッション開始要求（Client/Host → Host）
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_RequestStartSealCostChoice(PlayerRef requester, bool openToBoth)
    {
        if (!Object.HasStateAuthority) return;
        if (_isGameOver) return;

        // すでに進行中なら無視（多重開始防止）
        if (_sealSessionActive) return;

        _sealSessionActive = true;
        _sealSessionExpectBoth = openToBoth;
        _sealSessionId++;

        _sealSubmittedP0 = false;
        _sealSubmittedP1 = false;
        _sealChoiceP0 = -1;
        _sealChoiceP1 = -1;

        // 開く相手：SELFなら requester のみ、BOTHなら両方
        RPC_OpenSealCostChoice(_sealSessionId, requester, openToBoth);
    }

    // UIを開く（Host → All）
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_OpenSealCostChoice(int sessionId, PlayerRef requester, bool openToBoth)
    {
        _localSealSessionId = sessionId;
        _localSealResolvedSessionId = -1;

        // 自分のPlayerManager（InputAuthority）のUIだけ開く
        var localPm = GetLocalPlayerManager();
        if (localPm == null || localPm.costSealDeclareUI == null) return;

        // BOTHなら全員開く（各端末は自分のCanvasしか出てないので結果的に自分だけ開く）
        // SELFなら requester本人の端末だけ開く
        if (openToBoth)
        {
            localPm.costSealDeclareUI.Open(sessionId);
        }
        else
        {
            if (runner == null) runner = FindAnyObjectByType<NetworkRunner>();
            if (runner != null && runner.LocalPlayer == requester)
            {
                localPm.costSealDeclareUI.Open(sessionId);
            }
        }
    }

    // ローカルのPlayerManager取得（自分視点）
    private PlayerManager GetLocalPlayerManager()
    {
        if (players == null) return null;
        for (int i = 0; i < players.Count; i++)
        {
            var pm = players[i];
            if (pm != null && pm.Object != null && pm.Object.HasInputAuthority)
                return pm;
        }
        return null;
    }


    // RPC_OpenBlockChoice のすぐ後あたりに追加するのが分かりやすい
    // ============================================================
    //  ★追加：手札捨て選択UIを開く（SelectDiscardSelf用）
    //  - broadcastするが、実際にUIを開くのは inputAuthority のクライアントだけ
    // ============================================================
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_OpenSelectDiscardSelf(int targetIndex, int discardCount)
    {
        if (players == null) return;
        if (targetIndex < 0 || targetIndex >= players.Count) return;

        var ui = FindAnyObjectByType<HandDiscardSelectManager>();
        if (ui == null) return;

        ui.StartSelectDiscardMode(players[targetIndex], discardCount);
    }

    // RPC_RequestAddDiscard の直前あたりに追加
    // ============================================================
    //  ★追加：Host手札リストからカードを消して捨て札へ（BlockなどHost処理で使う用）
    //  - RPCの待ちでhandCountがズレないよう「即時に」更新する
    // ============================================================
    public void ConsumeHandCardToDiscardHost(PlayerRef ownerRef, int cardId)
    {
        if (Object == null || !Object.HasStateAuthority) return;

        int ownerIndex = FindPlayerIndexByRef(ownerRef);
        if (ownerIndex < 0) return;

        var list = GetHostHandIdList(ownerIndex);
        if (list == null) return;

        // 既に消えている場合は二重追加を避ける
        if (!list.Remove(cardId)) return;

        if (players != null && ownerIndex >= 0 && ownerIndex < players.Count && players[ownerIndex] != null)
            players[ownerIndex].handCount = list.Count;

        RPC_SyncAddDiscard(cardId);
    }

    // ============================================================
    //  ★追加：使用確定した手札カードを「手札IDリスト」から先に抜く
    //  - SwapHands / ハンデス / ランダム破壊 などで「使用中カード」を対象に含めないため
    //  - 捨て札へ入れるのは従来どおり効果終了時（RPC_RequestAddDiscard）
    // ============================================================
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_RequestConsumeHandCardForUse(PlayerRef requester, int cardId)
    {
        if (!Object.HasStateAuthority) return;
        if (_isGameOver) return;
        if (players == null || players.Count < 2) return;
        if (cardId <= 0) return;

        int pIndex = FindPlayerIndexByRef(requester);
        if (pIndex < 0) return;

        var list = GetHostHandIdList(pIndex);
        if (list == null) return;

        // 既に抜けているなら何もしない（多重呼び出し保険）
        if (!list.Remove(cardId)) return;

        if (players[pIndex] != null)
            players[pIndex].handCount = list.Count;
    }


    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_OpenChoiceMulti(PlayerRef chooserRef, int sessionId, int sourceCardId, string rawValue)
    {
        var r = runner;
        if (r == null) r = FindAnyObjectByType<NetworkRunner>();
        if (r == null) return;

        // 選択者以外は閉じる
        if (r.LocalPlayer != chooserRef)
        {
            var w = MultiChoiceWindow.Get();
            if (w != null) w.Hide();
            return;
        }

        int chooserIndex = FindPlayerIndexByRef(chooserRef);
        if (chooserIndex < 0) return;

        StartCoroutine(Co_ChoiceMultiChoice(sessionId, chooserIndex, sourceCardId, rawValue));
    }

    private IEnumerator Co_ChoiceMultiChoice(int sessionId, int chooserIndex, int sourceCardId, string rawValue)
    {
        var r = runner;
        if (r == null) r = FindAnyObjectByType<NetworkRunner>();
        if (r == null) yield break;

        var window = MultiChoiceWindow.Get();
        if (window == null) yield break;

        // UI用に rawValue を解析（選択回数/同一上限/文章だけ）
        if (!TryParseChoiceMultiValue_ForUI(rawValue, out int pickMax, out int sameMax, out string[] optionTexts))
        {
            // 解析できない場合は空で返す（Host側で無視）
            RPC_SubmitChoiceMulti(sessionId, r.LocalPlayer, new int[0]);
            yield break;
        }

        // カード本文（CSVのText）を表示
        string fullText = "";

        if (deckManager == null) deckManager = FindAnyObjectByType<DeckManager>();
        if (deckManager != null && sourceCardId >= 0)
        {
            var data = deckManager.GetCardDataById(sourceCardId);
            if (data != null) fullText = data.text;
        }

        bool confirmed = false;
        int[] pickedCounts = null;

        window.Open(fullText, optionTexts, pickMax, sameMax, (arr) =>
        {
            pickedCounts = arr;
            confirmed = true;
        });

        yield return new WaitUntil(() => confirmed && pickedCounts != null);

        RPC_SubmitChoiceMulti(sessionId, r.LocalPlayer, pickedCounts);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_SubmitChoiceMulti(int sessionId, PlayerRef chooserRef, int[] pickedCounts)
    {
        if (battleManager == null) return;
        battleManager.ReceiveChoiceMultiResult(sessionId, chooserRef, pickedCounts);
    }

    private bool TryParseChoiceMultiValue_ForUI(string raw, out int pickMax, out int sameMax, out string[] optionTexts)
    {
        pickMax = 0;
        sameMax = 0;
        optionTexts = null;

        if (string.IsNullOrEmpty(raw)) return false;

        List<string> opts = new List<string>();

        string[] parts = raw.Split(new char[] { ';', '；', '\n' }, System.StringSplitOptions.RemoveEmptyEntries);

        foreach (var p in parts)
        {
            string s = p.Trim();
            if (string.IsNullOrEmpty(s)) continue;

            if (s.StartsWith("P="))
            {
                int.TryParse(s.Substring(2), out pickMax);
                continue;
            }
            if (s.StartsWith("S="))
            {
                int.TryParse(s.Substring(2), out sameMax);
                continue;
            }

            // O1=文章=>効果... の「文章」だけ拾う
            if (s.StartsWith("O"))
            {
                int eq = s.IndexOf('=');
                string body = (eq >= 0) ? s.Substring(eq + 1) : s;

                int arrow = body.IndexOf("=>");
                string text = (arrow >= 0) ? body.Substring(0, arrow) : body;

                text = text.Trim();
                if (!string.IsNullOrEmpty(text))
                    opts.Add(text);
            }
        }

        if (pickMax <= 0) pickMax = 1;
        if (sameMax <= 0) sameMax = 1;

        if (opts.Count <= 0) return false;

        optionTexts = opts.ToArray();
        return true;
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_RequestStealRandomOpponent(PlayerRef thiefRef, PlayerRef victimRef, int count)
    {
        if (!Object.HasStateAuthority) return;
        if (_isGameOver) return;

        int thiefIndex = FindPlayerIndexByRef(thiefRef);
        int victimIndex = FindPlayerIndexByRef(victimRef);
        if (thiefIndex < 0 || victimIndex < 0) return;
        if (thiefIndex == victimIndex) return;

        if (count <= 0) return;

        var victimList = GetHostHandIdList(victimIndex);
        var thiefList = GetHostHandIdList(thiefIndex);
        if (victimList == null || thiefList == null) return;
        if (victimList.Count == 0) return;

        int n = Mathf.Min(count, victimList.Count);

        List<int> stolen = new List<int>(n);

        // Hostがランダムに確定：相手手札 → 自分手札（捨て札には入れない）
        for (int i = 0; i < n; i++)
        {
            int idx = UnityEngine.Random.Range(0, victimList.Count);
            int id = victimList[idx];

            victimList.RemoveAt(idx);
            thiefList.Add(id);

            stolen.Add(id);
        }

        // handCount確定（Networked）
        if (players[victimIndex] != null) players[victimIndex].handCount = victimList.Count;
        if (players[thiefIndex] != null) players[thiefIndex].handCount = thiefList.Count;

        // 同期（表示＋手札実体増減）
        RPC_SyncStealRandomOpponent(thiefIndex, victimIndex, stolen.ToArray());

        // ★重要：奪ってEX_001が揃うケースの勝利判定
        TryCheckEx001Win(thiefIndex);
    }



    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_SyncStealRandomOpponent(int thiefIndex, int victimIndex, int[] stolenIds)
    {
        if (stolenIds == null || stolenIds.Length == 0) return;
        if (players == null) return;
        if (thiefIndex < 0 || thiefIndex >= players.Count) return;
        if (victimIndex < 0 || victimIndex >= players.Count) return;

        if (deckManager == null) deckManager = FindAnyObjectByType<DeckManager>();

        var thiefPM = players[thiefIndex];
        var victimPM = players[victimIndex];
        if (thiefPM == null || victimPM == null) return;

        // ★SFX：奪う（両者）
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlaySfxBurst(SfxClipId.CardMove, stolenIds.Length);

        // 1) Steal専用UI表示（ローカルだけ文言を変える）
        if (CardMovePopupManager.Instance != null)
        {
            if (thiefPM.Object != null && thiefPM.Object.HasInputAuthority)
            {
                CardMovePopupManager.Instance.ShowStealCards(stolenIds, true);   // 奪った側
            }
            if (victimPM.Object != null && victimPM.Object.HasInputAuthority)
            {
                CardMovePopupManager.Instance.ShowStealCards(stolenIds, false); // 奪われた側
            }
        }

        // 2) 奪われた側（本人端末だけ）：手札実体を消す（捨て札には入れない）
        if (victimPM.Object != null && victimPM.Object.HasInputAuthority && victimPM.handManager != null)
        {
            foreach (int id in stolenIds)
            {
                RemoveLocalHandCardById(victimPM, id);
            }

            victimPM.handManager.UpdateCardPositions();
            victimPM.UpdateHandCountUI();
            victimPM.NotifyHandChangedForBothSides();
        }

        // 3) 奪った側（本人端末だけ）：手札実体を追加
        if (thiefPM.Object != null && thiefPM.Object.HasInputAuthority && thiefPM.handManager != null)
        {
            if (deckManager != null)
            {
                foreach (int id in stolenIds)
                {
                    var data = deckManager.GetCardDataById(id);
                    if (data != null)
                        thiefPM.handManager.AddCardFromData(data);
                }
            }

            thiefPM.handManager.UpdateCardPositions();
            thiefPM.UpdateHandCountUI();
            thiefPM.NotifyHandChangedForBothSides();
        }
    }


    public bool IsLifeDefenceSealedForIndex(int idx)
    {
        if (idx == 0) return _lifeDefenceSealedP0;
        if (idx == 1) return _lifeDefenceSealedP1;
        return false;
    }

    public bool IsLifeDefenceSealed(PlayerManager pm)
    {
        if (pm == null || players == null) return false;
        int idx = players.IndexOf(pm);
        return IsLifeDefenceSealedForIndex(idx);
    }

    // ==================================================
    //  ライフゾーンDEFENCE封印（永続）
    //  targetMode: 0=SELF / 1=OPPONENT / 2=BOTH
    // ==================================================
    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_RequestApplyLifeDefenceSeal(PlayerRef requester, int targetMode)
    {
        if (!Object.HasStateAuthority) return;
        if (players == null || players.Count < 2) return;

        int requesterIndex = FindPlayerIndexByRef(requester);
        if (requesterIndex < 0) return;

        if (targetMode == 0)
        {
            // SELF
            ApplyLifeDefenceSealHost(requesterIndex, true);
        }
        else if (targetMode == 1)
        {
            // OPPONENT
            int opp = (requesterIndex == 0) ? 1 : 0;
            ApplyLifeDefenceSealHost(opp, true);
        }
        else if (targetMode == 2)
        {
            // BOTH
            ApplyLifeDefenceSealHost(0, true);
            ApplyLifeDefenceSealHost(1, true);
        }
    }

    private void ApplyLifeDefenceSealHost(int targetIndex, bool sealedOn)
    {
        if (targetIndex == 0) _lifeDefenceSealedP0 = sealedOn;
        else if (targetIndex == 1) _lifeDefenceSealedP1 = sealedOn;

        RPC_ApplyLifeDefenceSeal(targetIndex, sealedOn);
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_ApplyLifeDefenceSeal(int playerIndex, bool sealedOn)
    {
        if (playerIndex == 0) _lifeDefenceSealedP0 = sealedOn;
        else if (playerIndex == 1) _lifeDefenceSealedP1 = sealedOn;

        // UI反映（自分視点）
        var localPm = GetLocalPlayerManager();
        if (localPm == null) return;

        // ★毎回「自分」と「相手」両方の封印状態を反映する（片側だけ更新されない事故防止）
        int localIdx = (players != null) ? players.IndexOf(localPm) : -1;
        if (localIdx < 0) return;

        int oppIdx = (localIdx == 0) ? 1 : 0;

        bool localSealed = IsLifeDefenceSealedForIndex(localIdx);
        bool oppSealed = IsLifeDefenceSealedForIndex(oppIdx);

        if (localPm.lifeManager != null)
            localPm.lifeManager.SetLifeDefenceSealed(localSealed);

        localPm.SetOpponentLifeDefenceSealed(oppSealed);

        // ★相手ライフ裏面の生成/整列も含めて再描画
        localPm.UpdateLifeUI();
    }

    public void RequestRetire()
    {
        if (_isGameOver) return;

        var r = runner;
        if (r == null) r = FindAnyObjectByType<NetworkRunner>();

        // 通信が死んでる/無いなら、ここでは何もしない（UI側でローカル敗北表示に落とす）
        if (r == null || !r.IsRunning) return;

        RPC_RequestRetire(r.LocalPlayer);
    }

    [Rpc(RpcSources.All, RpcTargets.StateAuthority)]
    public void RPC_RequestRetire(PlayerRef requester)
    {
        if (!Object.HasStateAuthority) return;
        if (_isGameOver) return;

        int loserIndex = FindPlayerIndexByRef(requester);

        int winnerIndex = 0;
        if (loserIndex == 0) winnerIndex = 1;
        else if (loserIndex == 1) winnerIndex = 0;

        EndGameInternal(winnerIndex, REASON_KEY_RETIRE);
    }






    // ============================================================
    //  Fusion 推奨：RenderでNetworkedの変更監視
    // ============================================================
    public override void Render()
    {
        if (players.Count < 2) return;

        if (currentPlayerIndex != _prevPlayerIndex)
        {
            int oldIndex = _prevPlayerIndex;

            // ★SFX：ターン終了（初回は鳴らさない）
            if (oldIndex != -1)
            {
                if (AudioManager.Instance != null)
                    AudioManager.Instance.PlaySfx(SfxClipId.TurnEnd);
            }

            _prevPlayerIndex = currentPlayerIndex;
            StartTurnInternal();

            // ★SFX：ターン開始
            if (AudioManager.Instance != null)
                AudioManager.Instance.PlaySfx(SfxClipId.TurnStart);
        }
    }

}
