using Fusion;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static CardGenerator;
using System.Linq;


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

    // ▼追加：勝敗状態
    private bool _isGameOver = false;
    public bool IsGameOver => _isGameOver;

    private const string EX_CARD_NAME = "EX_001";
    private const int EX_WIN_COUNT = 5;

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

        if (turnChoicePanel != null) turnChoicePanel.SetActive(false);
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

            // 変更後（Next待ちしない）
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
        // ★追加：Hostが handCount(Networked) を確定（これをしないと opponent.handCount が 0 のままになる）
        if (Object.HasStateAuthority)
        {
            if (players[0] != null && players[0].handManager != null)
                players[0].handCount = players[0].handManager.CardCount;

            if (players[1] != null && players[1].handManager != null)
                players[1].handCount = players[1].handManager.CardCount;
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
        // Host はすでに RemoveLife 済みなので、ここでは何もしない
        if (Object.HasStateAuthority)
            return;

        if (players == null || playerIndex < 0 || playerIndex >= players.Count)
            return;

        var pm = players[playerIndex];
        if (pm != null && pm.lifeManager != null)
        {
            pm.lifeManager.RemoveLife();
        }
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
        var data = deckManager.GetCardDataById(cardId);
        pm.handManager.AddCardFromData(data);

        // ★ Host only: ドローで手札ID追加＆handCount確定
        if (Object.HasStateAuthority)
        {
            var list = GetHostHandIdList(playerIndex);
            list.Add(cardId);

            if (pm != null) pm.handCount = list.Count;
            TryCheckEx001Win(playerIndex);
        }


        // ★追加：Hostが handCount(Networked) を確定（ドローで増えた分を即同期）
        if (Object.HasStateAuthority)
        {
            pm.handCount = pm.handManager.CardCount;
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
        // ★ 実際のマナ計算（Max+1 / Reset）を適用
        ApplyChargeInternal(playerIndex);

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
    // ★ Attack完了通知：Host -> 全員
    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_AttackResolved(PlayerRef attackerRef, int requestId)
    {
        // attacker本人の端末だけが、このrequestIdを完了として受け取ればOK
        if (runner == null) runner = FindAnyObjectByType<NetworkRunner>();
        if (runner == null) return;

        if (runner.LocalPlayer != attackerRef) return;

        CardGenerator.NotifyAttackResolved(requestId);
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

        targetPlayer.IncreaseMaxManaOnly(amount);

        // Networked値が変わったので、全員のUIを補正
        UpdateAllManaUI();
    }
    // Host 側のみ: 特定プレイヤーの最大マナを減らす（カード効果用）
    public void EffectManaReduce(PlayerManager targetPlayer, int amount)
    {
        if (!Object.HasStateAuthority) return;
        if (targetPlayer == null) return;
        if (amount <= 0) return;

        targetPlayer.DecreaseMaxManaOnly(amount);

        // Networked値が変わったので、全員のUIを補正
        UpdateAllManaUI();
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

        if (isAll)
        {
            targetPlayer.ResetMana();
        }
        else
        {
            targetPlayer.GainMana(amount);
        }

        UpdateAllManaUI();
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

        target.currentMana -= cost;

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
    public void RPC_RequestRecoverDiscard(PlayerRef requester, int[] recoverIds)
    {
        if (!Object.HasStateAuthority) return;
        if (recoverIds == null || recoverIds.Length == 0) return;

        if (deckManager == null) deckManager = FindAnyObjectByType<DeckManager>();
        if (discardManager == null) discardManager = FindAnyObjectByType<DiscardManager>();
        if (deckManager == null || discardManager == null) return;

        int playerIndex = FindPlayerIndexByRef(requester);
        if (playerIndex < 0 || playerIndex >= players.Count) return;

        // Hostが捨て札から実在する分だけ確定して回収させる
        List<int> accepted = new List<int>();

        foreach (int id in recoverIds)
        {
            var toRemove = discardManager.discardDataList.FirstOrDefault(d => d != null && d.id == id);
            if (toRemove != null)
            {
                discardManager.discardDataList.Remove(toRemove);
                accepted.Add(id);
            }
        }

        if (accepted.Count == 0) return;

        // ▼追加：Hostの手札IDリストにも反映（EX判定のため）
        var list = GetHostHandIdList(playerIndex);
        foreach (var id in accepted)
            list.Add(id);

        var pm = players[playerIndex];
        if (pm != null) pm.handCount = list.Count;

        // ▼追加：EX_001勝利チェック（手札に入った瞬間）
        TryCheckEx001Win(playerIndex);

        RPC_SyncRecoverDiscard(playerIndex, accepted.ToArray());
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_SyncRecoverDiscard(int playerIndex, int[] recoverIds)
    {
        if (deckManager == null) deckManager = FindAnyObjectByType<DeckManager>();
        if (discardManager == null) discardManager = FindAnyObjectByType<DiscardManager>();
        if (deckManager == null || discardManager == null) return;

        if (players == null || players.Count < 2) return;
        if (playerIndex < 0 || playerIndex >= players.Count) return;

        var pm = players[playerIndex];
        if (pm == null || pm.handManager == null) return;

        foreach (int id in recoverIds)
        {
            // まず捨て札から削除（自分のクライアントは既に除外済みでもOK）
            discardManager.RemoveFromDiscardById(id);

            var data = deckManager.GetCardDataById(id);
            if (data == null) continue;

            // 手札へ追加（各クライアントで同じPlayerManagerのHandManagerに追加）
            pm.handManager.AddCardFromData(data);
        }
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

        // 1) 共有捨て札へ追加（全員で同じ結果）
        if (deckManager != null && discardManager != null)
        {
            foreach (int id in cardIds)
            {
                var data = deckManager.GetCardDataById(id);
                if (data != null) discardManager.AddToDiscard(data);
            }
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
        foreach (var id in list)
        {
            var data = deckManager.GetCardDataById(id);
            if (data != null && data.name == EX_CARD_NAME)
                exCount++;
        }

        if (exCount >= EX_WIN_COUNT)
        {
            EndGameInternal(playerIndex, "EX_001が5枚揃った");
            return true;
        }

        return false;
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    private void RPC_GameOver(int winnerIndex, string reason)
    {
        _isGameOver = true;

        string msg = (winnerIndex < 0)
            ? $"引き分け：{reason}"
            : $"勝利：P{winnerIndex + 1}（{reason}）";

        if (turnChoicePanel != null)
            turnChoicePanel.SetActive(false);

        if (effectWindow != null)
        {
            StartCoroutine(effectWindow.ShowProcessAuto(msg, 2.0f, false));
        }
        else
        {
            Debug.Log(msg);
        }
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
