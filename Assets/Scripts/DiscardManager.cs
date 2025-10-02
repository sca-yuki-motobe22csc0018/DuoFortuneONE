using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using TMPro;

public class DiscardManager : MonoBehaviour
{
    [Header("Stored Discard Data (CardData only)")]
    public List<CardGenerator.CardData> discardDataList = new List<CardGenerator.CardData>();

    [Header("Full View UI")]
    public GameObject fullViewPanel;
    public Transform gridParent;
    public GameObject cardDisplayPrefab;
    public GameObject closeButton;
    public TMP_Text discardMessage;
    private Coroutine discardMessageRoutine;

    [Header("回収用UI")]
    public GameObject completeButton;
    public GameObject confirmPanel;
    public Transform confirmListParent;
    public GameObject confirmCardPrefab;
    public TMP_Text confirmMessage;
    public TMP_Text recoverLimitText;

    [Header("回収ゾーンUI")]
    public GameObject recoverZonePanel;
    public Transform recoverZoneParent;

    [Header("プレイ用Prefab")]
    public GameObject cardPlayablePrefab;

    [Header("ScrollView サイズ・位置調整")]
    public RectTransform discardScrollView;
    public Vector2 normalSize = new Vector2(0, 600);
    public Vector2 normalPos = new Vector2(0, 0);
    public Vector2 recoverSize = new Vector2(0, 300);
    public Vector2 recoverPos = new Vector2(0, 150);

    private bool isOpen = false;
    private bool isRecoverMode = false;
    private int recoverCount = 0;
    private PlayerManager recoverTargetPlayer;
    private List<CardGenerator.CardData> selectedCards = new List<CardGenerator.CardData>();

    public bool IsRecoverMode => isRecoverMode;

    void Start()
    {
        if (fullViewPanel != null) fullViewPanel.SetActive(false);
        if (completeButton != null) completeButton.SetActive(false);
        if (confirmPanel != null) confirmPanel.SetActive(false);
        if (recoverZonePanel != null) recoverZonePanel.SetActive(false);
        if (recoverLimitText != null) recoverLimitText.gameObject.SetActive(false);
    }

    public void AddToDiscard(CardGenerator.CardData data)
    {
        discardDataList.Add(data);
        BuildFullView();
    }

    public void BuildFullView()
    {
        if (gridParent == null || cardDisplayPrefab == null) return;

        foreach (Transform child in gridParent)
        {
            Destroy(child.gameObject);
        }

        var grouped = discardDataList.GroupBy(c => c.id);
        foreach (var g in grouped)
        {
            GameObject obj = Instantiate(cardDisplayPrefab, gridParent);
            CardUI ui = obj.GetComponent<CardUI>();
            if (ui != null)
            {
                ui.SetCard(g.First(), g.Count(), this, CardUISource.DiscardZone);
            }
        }
    }

    public void OpenDiscardPanel()
    {
        if (fullViewPanel != null) fullViewPanel.SetActive(true);
        isOpen = true;
        BuildFullView();
    }

    public void CloseDiscardPanel()
    {
        if (fullViewPanel != null) fullViewPanel.SetActive(false);
        isOpen = false;
    }

    public void StartRecoverMode(PlayerManager player, int count)
    {
        recoverTargetPlayer = player;
        recoverCount = count;
        isRecoverMode = true;
        selectedCards.Clear();

        if (completeButton != null) completeButton.SetActive(true);
        if (recoverZonePanel != null) recoverZonePanel.SetActive(true);
        if (recoverLimitText != null)
        {
            recoverLimitText.gameObject.SetActive(true);
            recoverLimitText.text = $"回収上限: {recoverCount}";
        }

        if (discardScrollView != null)
        {
            discardScrollView.sizeDelta = recoverSize;
            discardScrollView.anchoredPosition = recoverPos;
        }
    }

    public void EndRecoverMode()
    {
        isRecoverMode = false;
        selectedCards.Clear();

        if (completeButton != null) completeButton.SetActive(false);
        if (recoverZonePanel != null) recoverZonePanel.SetActive(false);
        if (recoverLimitText != null) recoverLimitText.gameObject.SetActive(false);

        if (discardScrollView != null)
        {
            discardScrollView.sizeDelta = normalSize;
            discardScrollView.anchoredPosition = normalPos;
        }
    }

    public void MoveCardToRecover(CardGenerator.CardData data)
    {
        if (!isRecoverMode) return;

        if (selectedCards.Count >= recoverCount)
        {
            if (discardMessageRoutine != null) StopCoroutine(discardMessageRoutine);
            discardMessageRoutine = StartCoroutine(ShowDiscardMessage($"指定枚数（{recoverCount}枚）より多く選択しています！"));
            return;
        }

        selectedCards.Add(data);
        BuildRecoverZone();
    }

    public void MoveCardBackToDiscard(CardGenerator.CardData data)
    {
        if (!isRecoverMode) return;

        selectedCards.Remove(data);
        BuildRecoverZone();
    }

    public void BuildRecoverZone()
    {
        foreach (Transform child in recoverZoneParent)
        {
            Destroy(child.gameObject);
        }

        foreach (var card in selectedCards)
        {
            GameObject obj = Instantiate(cardDisplayPrefab, recoverZoneParent);
            CardUI ui = obj.GetComponent<CardUI>();
            if (ui != null)
            {
                ui.SetCard(card, 1, this, CardUISource.RecoverZone);
            }
        }
    }

    public void OnCompleteButton()
    {
        if (confirmPanel != null) confirmPanel.SetActive(true);
        if (confirmMessage != null) confirmMessage.text = "これらのカードを回収しますか？";

        foreach (Transform child in confirmListParent)
        {
            Destroy(child.gameObject);
        }

        foreach (var card in selectedCards)
        {
            GameObject obj = Instantiate(confirmCardPrefab, confirmListParent);
            CardUI ui = obj.GetComponent<CardUI>();
            if (ui != null) ui.SetCard(card, 1);
        }
    }

    public void OnConfirmOK()
    {
        if (recoverTargetPlayer != null)
        {
            foreach (var card in selectedCards)
            {
                GameObject obj = Instantiate(cardPlayablePrefab, recoverTargetPlayer.handManager.transform);
                CardGenerator cg = obj.GetComponent<CardGenerator>();
                if (cg != null) cg.ApplyCardData(card);
                recoverTargetPlayer.handManager.handCards.Add(obj);
            }
            recoverTargetPlayer.handManager.UpdateCardPositions();
        }

        EndRecoverMode();
        if (confirmPanel != null) confirmPanel.SetActive(false);
    }

    public void OnConfirmCancel()
    {
        if (confirmPanel != null) confirmPanel.SetActive(false);
    }

    IEnumerator ShowDiscardMessage(string msg)
    {
        if (discardMessage != null)
        {
            discardMessage.text = msg;
            discardMessage.gameObject.SetActive(true);
            yield return new WaitForSeconds(1f);
            discardMessage.gameObject.SetActive(false);
        }
    }
}
