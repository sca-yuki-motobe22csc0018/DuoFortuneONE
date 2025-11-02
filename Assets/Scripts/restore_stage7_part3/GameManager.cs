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

    [Networked] public int FirstPlayerIndex { get; private set; } = -1;

    private PlayerManager currentPlayer;
    private int turnNumber = 1;
    private NetworkRunner runner;

    private void Start()
    {
        runner = FindAnyObjectByType<NetworkRunner>();

        turnChoicePanel.SetActive(false);
        drawButton.onClick.AddListener(OnDrawSelected);
        increaseManaButton.onClick.AddListener(OnIncreaseManaSelected);
        endTurnButton.onClick.AddListener(OnEndTurn);
        endTurnButton.interactable = false;

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

        // --- Hostが先攻を決定 ---
        if (Object.HasStateAuthority)
        {
            int mode = LobbyManager.SelectedTurnMode;

            switch (mode)
            {
                case 0: // ランダム
                    FirstPlayerIndex = Random.Range(0, 2);
                    Debug.Log($"[Host] ランダム決定 → {(FirstPlayerIndex == 0 ? "Host先攻" : "Client先攻")}");
                    break;
                case 1: // 先攻固定
                    FirstPlayerIndex = 0;
                    Debug.Log("[Host] 先攻: Host");
                    break;
                case 2: // 後攻固定
                    FirstPlayerIndex = 1;
                    Debug.Log("[Host] 先攻: Client");
                    break;
            }
        }

        Invoke(nameof(StartGameTurn), 0.5f);
    }

    private void StartGameTurn()
    {
        bool isHost = runner.IsServer;
        bool isMyTurn = (isHost && FirstPlayerIndex == 0) || (!isHost && FirstPlayerIndex == 1);

        currentPlayer = isMyTurn ? player1 : player2;
        Debug.Log(isMyTurn ? "[GameManager] あなたのターン開始！" : "[GameManager] 相手のターンです。");

        turnNumber = 1;
        StartTurn(currentPlayer);
    }

    private void StartTurn(PlayerManager player)
    {
        currentPlayer = player;
        endTurnButton.interactable = true;
        turnChoicePanel.SetActive(true);

        if (turnInfoText != null)
            turnInfoText.text = $"Turn {turnNumber}: {player.name}";
    }

    private void OnDrawSelected()
    {
        if (deckManager != null && currentPlayer != null)
        {
            deckManager.DrawCardToHand(currentPlayer);
            deckManager.DrawCardToHand(currentPlayer);
        }

        currentPlayer.ResetMana();
        turnChoicePanel.SetActive(false);
    }

    private void OnIncreaseManaSelected()
    {
        if (currentPlayer != null)
        {
            currentPlayer.IncreaseMaxMana(1);
            currentPlayer.ResetMana();
        }

        turnChoicePanel.SetActive(false);
    }

    public void OnEndTurn()
    {
        turnChoicePanel.SetActive(false);
        endTurnButton.interactable = false;
        NextTurn();
    }

    private void NextTurn()
    {
        currentPlayer = (currentPlayer == player1) ? player2 : player1;

        if (currentPlayer == player1)
            turnNumber++;

        StartTurn(currentPlayer);
    }

    public bool IsMyTurn(PlayerManager p) => currentPlayer == p;
}
