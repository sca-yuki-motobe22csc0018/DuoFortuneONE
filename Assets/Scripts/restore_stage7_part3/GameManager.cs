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
                pm.UpdateHandCountUI();
                pm.UpdateLifeUI();   // ★ 追加
            }
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
    public void RPC_RequestAttack(PlayerRef attackerRef, int attackCardId)
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
            battleManager.EnqueueAttack(attacker, defender, attackData);
        }
    }

    [Rpc(RpcSources.StateAuthority, RpcTargets.All)]
    public void RPC_OpenBlockChoice(int defenderIndex)
    {
        if (players == null || defenderIndex < 0 || defenderIndex >= players.Count) return;

        // defender本人の端末だけ開く
        if (players[defenderIndex].Object != null && players[defenderIndex].Object.HasInputAuthority)
        {
            StartCoroutine(Co_BlockChoice(defenderIndex));
        }
        else
        {
            // それ以外の端末は念のため閉じる
            if (BlockWindow.Instance != null) BlockWindow.Instance.gameObject.SetActive(false);
        }
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
        if (r == null)
            r = FindAnyObjectByType<NetworkRunner>();

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
    public void RPC_OpenDefenceChoice(int defenderIndex, int destroyedLifeCardId)
    {
        if (players == null || defenderIndex < 0 || defenderIndex >= players.Count) return;

        if (players[defenderIndex].Object != null && players[defenderIndex].Object.HasInputAuthority)
        {
            StartCoroutine(Co_DefenceChoice(defenderIndex, destroyedLifeCardId));
        }
        else
        {
            if (DefenceWindow.Instance != null) DefenceWindow.Instance.gameObject.SetActive(false);
        }
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
