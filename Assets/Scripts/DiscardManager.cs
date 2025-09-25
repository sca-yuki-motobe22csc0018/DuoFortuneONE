using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class DiscardManager : MonoBehaviour
{
    [Header("Stored Discard Data (CardData only)")]
    public List<CardGenerator.CardData> discardDataList = new List<CardGenerator.CardData>();

    [Header("Full View UI")]
    public GameObject fullViewPanel;
    public Transform gridParent;
    public GameObject cardDisplayPrefab;
    public GameObject closeButton;
    public Text discardMessage; // �� DiscardUIPanel ���ɒu���e�L�X�g
    private Coroutine discardMessageRoutine; // �\�����R���[�`��

    [Header("����pUI")]
    public GameObject completeButton;
    public GameObject confirmPanel;
    public Transform confirmListParent;
    public GameObject confirmCardPrefab;
    public Text confirmMessage;
    public Text recoverLimitText;

    [Header("����]�[��UI")]
    public GameObject recoverZonePanel;   // �� ScrollView �{�́i�����͔�\���ɂ��Ă����j
    public Transform recoverZoneParent;   // �� Content ����

    [Header("�v���C�pPrefab")]
    public GameObject cardPlayablePrefab;

    [Header("ScrollView �T�C�Y�E�ʒu����")]
    public RectTransform discardScrollView;

    // �ʏ펞
    public Vector2 normalSize = new Vector2(0, 600);
    public Vector2 normalPos = new Vector2(0, 0);

    // ������[�h��
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
        if (recoverZonePanel != null) recoverZonePanel.SetActive(false); // �� �����͔�\��
        if (recoverLimitText != null) recoverLimitText.gameObject.SetActive(false); // �� �ǉ�
    }

    // �̂ĎD�ɒǉ�
    public void AddToDiscard(CardGenerator.CardData data)
    {
        if (data == null) return;
        discardDataList.Add(data);
        if (isOpen) BuildFullView();
    }

    // �̂ĎD�ꗗ�̊J��
    public void OnDiscardClicked()
    {
        isOpen = !isOpen;
        if (fullViewPanel == null || gridParent == null || cardDisplayPrefab == null) return;

        fullViewPanel.SetActive(isOpen);
        if (isOpen) BuildFullView();
        else ClearFullView();
    }

    private void BuildFullView()
    {
        ClearFullView();

        var grouped = discardDataList
            .Where(d => d != null)
            .GroupBy(d => d.id)
            .OrderBy(g => g.Key);

        foreach (var g in grouped)
        {
            var data = g.First();
            int count = g.Count();

            GameObject ui = Instantiate(cardDisplayPrefab, gridParent);

            var cardUI = ui.GetComponent<CardUI>();
            if (cardUI != null)
                cardUI.SetCard(data, count, this, CardUISource.DiscardZone);

            var detail = ui.GetComponent<CardUIDetail>();
            if (detail != null) detail.Init(data);
        }
    }

    private void ClearFullView()
    {
        for (int i = gridParent.childCount - 1; i >= 0; i--)
        {
            Destroy(gridParent.GetChild(i).gameObject);
        }
    }

    public void CloseDiscard()
    {
        isOpen = false;
        if (fullViewPanel != null)
            fullViewPanel.SetActive(false);
        ClearFullView();
    }

    // ������[�h�J�n
    public void StartRecoverMode(PlayerManager player, int count)
    {
        isRecoverMode = true;
        recoverCount = count;
        recoverTargetPlayer = player;
        selectedCards.Clear();

        if (completeButton) completeButton.SetActive(true);
        if (closeButton) closeButton.SetActive(false);
        if (confirmPanel) confirmPanel.SetActive(false);

        if (recoverLimitText != null)
        {
            recoverLimitText.gameObject.SetActive(true); // �� �\��
            recoverLimitText.text = $"����ł��閇���F{recoverCount}";
        }

        if (recoverZonePanel != null)
            recoverZonePanel.SetActive(true);

        if (discardScrollView != null)
        {
            discardScrollView.sizeDelta = recoverSize;
            discardScrollView.anchoredPosition = recoverPos;
        }

        fullViewPanel.SetActive(true);
        BuildFullView();
        BuildRecoverZone();
    }


    // ����]�[���\�z
    private void BuildRecoverZone()
    {
        foreach (Transform child in recoverZoneParent)
            Destroy(child.gameObject);

        var grouped = selectedCards
            .Where(d => d != null)
            .GroupBy(d => d.id)
            .OrderBy(g => g.Key);

        foreach (var g in grouped)
        {
            var data = g.First();
            int count = g.Count();

            GameObject ui = Instantiate(cardDisplayPrefab, recoverZoneParent);
            var cardUI = ui.GetComponent<CardUI>();
            if (cardUI != null)
                cardUI.SetCard(data, count, this, CardUISource.RecoverZone);

            var detail = ui.GetComponent<CardUIDetail>();
            if (detail != null) detail.Init(data);
        }
    }

    // �̂ĎD �� ���
    public void MoveCardToRecover(CardGenerator.CardData data)
    {
        var toRemove = discardDataList.FirstOrDefault(d => d.id == data.id);
        if (toRemove != null)
        {
            discardDataList.Remove(toRemove);
            selectedCards.Add(toRemove);

            BuildFullView();
            BuildRecoverZone();
        }
    }

    // ��� �� �̂ĎD
    public void MoveCardBackToDiscard(CardGenerator.CardData data)
    {
        var toRemove = selectedCards.FirstOrDefault(d => d != null && d.id == data.id);
        if (toRemove != null)
        {
            selectedCards.Remove(toRemove);
            discardDataList.Add(toRemove);

            BuildFullView();
            BuildRecoverZone();
        }
    }

    // �����{�^��
    public void OnCompleteButton()
    {
        if (confirmPanel == null) return;

        if (selectedCards.Count > recoverCount)
        {
            // �� �x�����ꎞ�\��
            ShowDiscardWarning($"�w�薇���i{recoverCount}���j��葽���I�����Ă��܂��I", 1f);
            return;
        }

        // �m�F��ʂɐi�ޑO�Ɍx��������
        if (discardMessage != null)
            discardMessage.gameObject.SetActive(false);

        confirmPanel.SetActive(true);

        foreach (Transform child in confirmListParent)
            Destroy(child.gameObject);

        var grouped = selectedCards
            .Where(d => d != null)
            .GroupBy(d => d.id)
            .OrderBy(g => g.Key);

        foreach (var g in grouped)
        {
            var data = g.First();
            int count = g.Count();

            var obj = Instantiate(confirmCardPrefab, confirmListParent);
            var ui = obj.GetComponent<CardUI>();
            if (ui != null) ui.SetCard(data, count, null);

            var detail = obj.GetComponent<CardUIDetail>();
            if (detail != null) detail.Init(data);
        }

        if (confirmMessage != null)
        {
            if (selectedCards.Count < recoverCount)
                confirmMessage.text = $"�܂��I�ׂ܂������v�ł����H�i{selectedCards.Count}/{recoverCount}�j";
            else
                confirmMessage.text = $"�����������܂����H�i{selectedCards.Count}/{recoverCount}�j";
        }
    }
    private void ShowDiscardWarning(string message, float duration = 1f)
    {
        if (discardMessage == null) return;

        // ���ɕ\�����Ȃ�~�߂�
        if (discardMessageRoutine != null)
            StopCoroutine(discardMessageRoutine);

        discardMessageRoutine = StartCoroutine(ShowDiscardWarningRoutine(message, duration));
    }

    private IEnumerator ShowDiscardWarningRoutine(string message, float duration)
    {
        discardMessage.gameObject.SetActive(true);
        discardMessage.text = message;

        yield return new WaitForSecondsRealtime(duration);

        discardMessage.text = "";
        discardMessage.gameObject.SetActive(false);
        discardMessageRoutine = null;
    }


    // OK
    public void OnConfirmOK()
    {
        if (cardPlayablePrefab == null)
        {
            Debug.LogError("cardPlayablePrefab �����ݒ�ł��BInspector�Ŋ��蓖�ĂĂ��������B");
            return;
        }

        foreach (var data in selectedCards)
        {
            var go = Instantiate(cardPlayablePrefab, recoverTargetPlayer.handManager.transform);
            var cg = go.GetComponent<CardGenerator>();
            cg.ApplyCardData(data);
            cg.player = recoverTargetPlayer;
            recoverTargetPlayer.handManager.AddCard(go);
        }

        EndRecoverMode();
    }

    // Cancel
    public void OnConfirmCancel()
    {
        if (confirmPanel) confirmPanel.SetActive(false);
    }

    // ������[�h�I��
    private void EndRecoverMode()
    {
        isRecoverMode = false;
        recoverCount = 0;
        recoverTargetPlayer = null;
        selectedCards.Clear();

        if (completeButton) completeButton.SetActive(false);
        if (closeButton) closeButton.SetActive(true);
        if (confirmPanel) confirmPanel.SetActive(false);
        if (fullViewPanel) fullViewPanel.SetActive(false);
        if (recoverZonePanel) recoverZonePanel.SetActive(false);

        if (recoverLimitText != null)
            recoverLimitText.gameObject.SetActive(false); // �� ��\��

        // ���T�C�Y�ƈʒu�����ɖ߂�
        if (discardScrollView != null)
        {
            discardScrollView.sizeDelta = normalSize;
            discardScrollView.anchoredPosition = normalPos;
        }
    }
}
