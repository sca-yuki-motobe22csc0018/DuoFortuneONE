using System.Collections;
using UnityEngine;
using UnityEngine.UI;

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
    public Text turnInfoText;   // ���^�[���\���p

    [Header("Initial Settings")]
    public int initialHandCount = 5;
    public int initialLifeCount = 3;

    private PlayerManager currentPlayer;
    private int turnNumber = 1; // ���^�[�����J�E���^�[

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

        if (lifeManager1 != null) lifeManager1.SetupInitialLife(initialLifeCount);
        if (lifeManager2 != null) lifeManager2.SetupInitialLife(initialLifeCount);

        if (deckManager != null)
        {
            for (int i = 0; i < initialHandCount; i++)
            {
                deckManager.DrawCardToHand(player1);
                deckManager.DrawCardToHand(player2);
            }
        }

        currentPlayer = player1;
        turnNumber = 1; // �ŏ��̃^�[��
        StartTurn(currentPlayer);
    }

    void StartTurn(PlayerManager player)
    {
        currentPlayer = player;

        // �� �����̃^�[���J�n���Ƀ}�i��S��
        currentPlayer.ResetMana();

        endTurnButton.interactable = true;
        turnChoicePanel.SetActive(true);

        // ���^�[�����ƃv���C���[��UI�ɔ��f
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
        turnChoicePanel.SetActive(false);
    }

    void OnIncreaseManaSelected()
    {
        if (currentPlayer != null)
        {
            currentPlayer.IncreaseMaxMana(1);
        }
        turnChoicePanel.SetActive(false);
    }

    public void OnEndTurn()
    {
        EndTurnChoice();
    }

    void EndTurnChoice()
    {
        turnChoicePanel.SetActive(false);

        if (currentPlayer != null)
        {
            currentPlayer.ResetMana();
        }

        Debug.Log($"�^�[���I����������: {currentPlayer.name}");

        endTurnButton.interactable = false;

        NextTurn();
    }

    void NextTurn()
    {
        currentPlayer = (currentPlayer == player1) ? player2 : player1;

        // ��Player1�̃^�[�������邽�тɃ^�[������i�߂�
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
