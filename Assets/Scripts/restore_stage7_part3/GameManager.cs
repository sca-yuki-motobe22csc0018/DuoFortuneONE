using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Fusion;

public class GameManager : NetworkBehaviour
{
    [Header("References")]
    public PlayerManager player1;
    public PlayerManager player2;
    public DeckManager deckManager;
    public LifeManager lifeManager1;
    public LifeManager lifeManager2;

    [Header("UI")]
    public GameObject turnChoicePanel;
    public Button drawButton;
    public Button increaseManaButton;
    public Button endTurnButton;
    public TMP_Text turnInfoText;

    [Header("Initial Settings")]
    public int initialHandCount = 5;
    public int initialLifeCount = 3;

    // 先攻（Host決定・共有）
    [Networked] public int FirstPlayerIndex { get; private set; } = -1;

    // 現在のターンプレイヤー（0 = Host側, 1 = Client側）
    [Networked] public int currentPlayerIndex { get; set; }

    private int _prevPlayerIndex = -1; // ★ 前回値を保持して手動で検知

    private PlayerManager currentPlayer;
    private int turnNumber = 1;
    private NetworkRunner runner;

    // ローカル端末のプレイヤーインデックス
    private int MyIndex => (runner != null && runner.IsServer) ? 0 : 1;
    private bool IsMyTurn() => currentPlayerIndex == MyIndex;

    private void Start()
    {
        runner = FindAnyObjectByType<NetworkRunner>();

        if (turnChoicePanel != null) turnChoicePanel.SetActive(false);
        if (drawButton != null) drawButton.onClick.AddListener(OnDrawSelected);
        if (increaseManaButton != null) increaseManaButton.onClick.AddListener(OnIncreaseManaSelected);
        if (endTurnButton != null) endTurnButton.onClick.AddListener(OnEndTurn);
        if (endTurnButton != null) endTurnButton.interactable = false;

        StartCoroutine(InitGameCoroutine());
    }

    private IEnumerator InitGameCoroutine()
    {
        // ★ Runnerが有効になるまで待つ
        while (NetworkRunner.GetRunnerForScene(gameObject.scene) == null)
            yield return null;

        // ★ PlayerManagerが確実に見つかるまで待つ
        while (FindObjectOfType<PlayerManager>() == null)
            yield return null;

        yield return null;

        // 山札・ライフ・手札初期化
        if (deckManager != null)
            deckManager.InitializeDeck();

        if (lifeManager1 != null)
            lifeManager1.SetupInitialLife(initialLifeCount, deckManager);
        if (lifeManager2 != null)
            lifeManager2.SetupInitialLife(initialLifeCount, deckManager);

        if (deckManager != null)
        {
            for (int i = 0; i < initialHandCount; i++)
            {
                deckManager.DrawCardToHand(player1);
                deckManager.DrawCardToHand(player2);
            }
        }

        // --- Hostが先攻を決定し共有 ---
        if (Object.HasStateAuthority)
        {
            int mode = LobbyManager.SelectedTurnMode;

            switch (mode)
            {
                case 0: // ランダム
                    FirstPlayerIndex = Random.Range(0, 2);
                    Debug.Log($"[Host] ランダム決定 → {(FirstPlayerIndex == 0 ? "Host先攻" : "Client先攻")}");
                    break;
                case 1: // Host先攻
                    FirstPlayerIndex = 0;
                    Debug.Log("[Host] 先攻: Host");
                    break;
                case 2: // Client先攻
                    FirstPlayerIndex = 1;
                    Debug.Log("[Host] 先攻: Client");
                    break;
            }
        }

        Invoke(nameof(StartGameTurn), 0.5f);
    }

    private void StartGameTurn()
    {
        if (Object.HasStateAuthority)
        {
            // Hostのみ初期ターン設定
            currentPlayerIndex = (FirstPlayerIndex >= 0) ? FirstPlayerIndex : 0;
        }

        // 初回反映
        ApplyTurnFromIndex();
        turnNumber = 1;
        _prevPlayerIndex = currentPlayerIndex;
    }

    // =========================
    //   ターン制御
    // =========================

    private void StartTurn(PlayerManager player)
    {
        currentPlayer = player;

        if (turnChoicePanel != null)
            turnChoicePanel.SetActive(IsMyTurn());

        if (endTurnButton != null)
            endTurnButton.interactable = IsMyTurn();

        if (turnInfoText != null)
            turnInfoText.text = $"Turn {turnNumber}: {player.name}";
    }

    private void OnDrawSelected()
    {
        if (!IsMyTurn()) return;

        if (deckManager != null && currentPlayer != null)
        {
            deckManager.DrawCardToHand(currentPlayer);
            deckManager.DrawCardToHand(currentPlayer);
        }

        if (currentPlayer != null) currentPlayer.ResetMana();
        if (turnChoicePanel != null) turnChoicePanel.SetActive(false);
    }

    private void OnIncreaseManaSelected()
    {
        if (!IsMyTurn()) return;

        if (currentPlayer != null)
        {
            currentPlayer.IncreaseMaxMana(1);
            currentPlayer.ResetMana();
        }

        if (turnChoicePanel != null) turnChoicePanel.SetActive(false);
    }

    public void OnEndTurn()
    {
        if (!IsMyTurn()) return;

        if (turnChoicePanel != null) turnChoicePanel.SetActive(false);
        if (endTurnButton != null) endTurnButton.interactable = false;

        if (Object.HasStateAuthority)
        {
            NextTurn();
        }
        else
        {
            Rpc_RequestNextTurn();
        }
    }

    // Hostのみが呼ぶ：ターンを進める
    private void NextTurn()
    {
        if (!Object.HasStateAuthority) return;

        // 終了時の処理があればここに
        // ...

        currentPlayerIndex = (currentPlayerIndex == 0) ? 1 : 0;
        if (currentPlayerIndex == 0)
            turnNumber++;
    }

    [Rpc(sources: RpcSources.All, targets: RpcTargets.StateAuthority)]
    private void Rpc_RequestNextTurn()
    {
        NextTurn();
    }

    // =========================
    //   手動でのターン変化検知
    // =========================
    public override void FixedUpdateNetwork()
    {
        // Fusion 2.0.8では OnChanged 廃止 → 手動比較
        if (currentPlayerIndex != _prevPlayerIndex)
        {
            _prevPlayerIndex = currentPlayerIndex;
            OnTurnIndexChanged();
        }
    }

    private void OnTurnIndexChanged()
    {
        ApplyTurnFromIndex();
    }

    private void ApplyTurnFromIndex()
    {
        currentPlayer = (currentPlayerIndex == 0) ? player1 : player2;

        if (turnChoicePanel != null)
            turnChoicePanel.SetActive(IsMyTurn());

        if (endTurnButton != null)
            endTurnButton.interactable = IsMyTurn();

        if (turnInfoText != null)
        {
            string youOrOpp = IsMyTurn() ? "あなたのターン" : "相手のターン";
            turnInfoText.text = $"Turn {turnNumber}: {currentPlayer.name}（{youOrOpp}）";
        }

        StartTurn(currentPlayer);
    }
}
