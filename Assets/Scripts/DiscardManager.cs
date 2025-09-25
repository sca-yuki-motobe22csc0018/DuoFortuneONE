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
    public Text discardMessage; // ← DiscardUIPanel 内に置くテキスト
    private Coroutine discardMessageRoutine; // 表示中コルーチン

    [Header("回収用UI")]
    public GameObject completeButton;
    public GameObject confirmPanel;
    public Transform confirmListParent;
    public GameObject confirmCardPrefab;
    public Text confirmMessage;
    public Text recoverLimitText;

    [Header("回収ゾーンUI")]
    public GameObject recoverZonePanel;   // ← ScrollView 本体（初期は非表示にしておく）
    public Transform recoverZoneParent;   // ← Content 部分

    [Header("プレイ用Prefab")]
    public GameObject cardPlayablePrefab;

    [Header("ScrollView サイズ・位置調整")]
    public RectTransform discardScrollView;

    // 通常時
    public Vector2 normalSize = new Vector2(0, 600);
    public Vector2 normalPos = new Vector2(0, 0);

    // 回収モード時
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
        if (recoverZonePanel != null) recoverZonePanel.SetActive(false); // ← 初期は非表示
        if (recoverLimitText != null) recoverLimitText.gameObject.SetActive(false); // ← 追加
    }

    // 捨て札に追加
    public void AddToDiscard(CardGenerator.CardData data)
    {
        if (data == null) return;
        discardDataList.Add(data);
        if (isOpen) BuildFullView();
    }

    // 捨て札一覧の開閉
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

    // 回収モード開始
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
            recoverLimitText.gameObject.SetActive(true); // ← 表示
            recoverLimitText.text = $"回収できる枚数：{recoverCount}";
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


    // 回収ゾーン構築
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

    // 捨て札 → 回収
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

    // 回収 → 捨て札
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

    // 完了ボタン
    public void OnCompleteButton()
    {
        if (confirmPanel == null) return;

        if (selectedCards.Count > recoverCount)
        {
            // ★ 警告を一時表示
            ShowDiscardWarning($"指定枚数（{recoverCount}枚）より多く選択しています！", 1f);
            return;
        }

        // 確認画面に進む前に警告を消す
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
                confirmMessage.text = $"まだ選べますが大丈夫ですか？（{selectedCards.Count}/{recoverCount}）";
            else
                confirmMessage.text = $"これを回収しますか？（{selectedCards.Count}/{recoverCount}）";
        }
    }
    private void ShowDiscardWarning(string message, float duration = 1f)
    {
        if (discardMessage == null) return;

        // 既に表示中なら止める
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
            Debug.LogError("cardPlayablePrefab が未設定です。Inspectorで割り当ててください。");
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

    // 回収モード終了
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
            recoverLimitText.gameObject.SetActive(false); // ← 非表示

        // ★サイズと位置を元に戻す
        if (discardScrollView != null)
        {
            discardScrollView.sizeDelta = normalSize;
            discardScrollView.anchoredPosition = normalPos;
        }
    }
}
