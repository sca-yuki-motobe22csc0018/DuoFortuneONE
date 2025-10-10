using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GameManager : MonoBehaviour
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

    private PlayerManager currentPlayer;
    private int turnNumber = 1;

    void Start()
    {
        turnChoicePanel.SetActive(false);

        drawButton.onClick.AddListener(OnDrawSelected);
        increaseManaButton.onClick.AddListener(OnIncreaseManaSelected);
        endTurnButton.onClick.AddListener(OnEndTurn);

        endTurnButton.interactable = false;

        StartCoroutine(InitGameCoroutine());
    }

    IEnumerator InitGameCoroutine()
    {
        yield return null;

        if (deckManager != null)
        {
            deckManager.InitializeDeck();
        }

        // ★ ここを deckManager を渡す形に修正
        if (lifeManager1 != null) lifeManager1.SetupInitialLife(initialLifeCount, deckManager);
        if (lifeManager2 != null) lifeManager2.SetupInitialLife(initialLifeCount, deckManager);

        if (deckManager != null)
        {
            for (int i = 0; i < initialHandCount; i++)
            {
                deckManager.DrawCardToHand(player1);
                deckManager.DrawCardToHand(player2);
            }
        }

        currentPlayer = player1;
        turnNumber = 1; // 最初のターン
        StartTurn(currentPlayer);
    }

    void StartTurn(PlayerManager player)
    {
        currentPlayer = player;

        endTurnButton.interactable = true;
        turnChoicePanel.SetActive(true);

        // ターン数とプレイヤーをUIに反映
        if (turnInfoText != null)
        {
            turnInfoText.text = $"Turn {turnNumber}: {player.name}";
        }
    }

    void OnDrawSelected()
    {
        if (deckManager != null && currentPlayer != null)
        {
            deckManager.DrawCardToHand(currentPlayer);
            deckManager.DrawCardToHand(currentPlayer);
        }
        // ★ ここで自分だけマナ全回復
        currentPlayer.ResetMana();
        turnChoicePanel.SetActive(false);
    }

    void OnIncreaseManaSelected()
    {
        if (currentPlayer != null)
        {
            currentPlayer.IncreaseMaxMana(1);
        }
        // ★ ここで自分だけマナ全回復
        currentPlayer.ResetMana();
        turnChoicePanel.SetActive(false);
    }

    public void OnEndTurn()
    {
        EndTurnChoice();
    }

    void EndTurnChoice()
    {
        turnChoicePanel.SetActive(false);

        endTurnButton.interactable = false;

        NextTurn();
    }

    void NextTurn()
    {
        currentPlayer = (currentPlayer == player1) ? player2 : player1;

        // Player1のターンが来るたびにターン数を進める
        if (currentPlayer == player1)
        {
            turnNumber++;
        }

        StartTurn(currentPlayer);
    }

    public bool IsMyTurn(PlayerManager p)
    {
        return currentPlayer == p;
    }
}
