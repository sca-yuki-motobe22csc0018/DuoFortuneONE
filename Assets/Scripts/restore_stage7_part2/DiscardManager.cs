using Fusion;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using Unity.VisualScripting;
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
    public TMP_Text discardMessage; // ← DiscardUIPanel 内に置くテキスト
    private Coroutine discardMessageRoutine; // 表示中コルーチン

    [Header("回収用UI")]
    public GameObject completeButton;
    public GameObject confirmPanel;
    public Transform confirmListParent;
    public GameObject confirmCardPrefab;
    public TMP_Text confirmMessage;
    public TMP_Text recoverLimitText;

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

    // 選択中の同ID枚数（回収モード表示用）
    private int GetSelectedCountById(int id)
    {
        return selectedCards.Count(d => d != null && d.id == id);
    }

    private bool isRecoverComplete = false;
    public bool IsRecoverComplete => isRecoverComplete;
    public bool IsRecoverMode => isRecoverMode;

    // ★追加：回収モードの「完了ボタン」を押せる条件を更新
    private void UpdateCompleteButton()
    {
        if (completeButton == null) return;

        var btn = completeButton.GetComponent<Button>();
        if (btn == null) return;

        int total = discardDataList.Count(d => d != null);
        int selected = selectedCards.Count(d => d != null);
        int remaining = total - selected;

        // recoverCount ぴったり もしくは 捨て札がもう残ってない（=全部選び切った）
        bool canGo = (recoverCount > 0) && (selected > 0) && (selected == recoverCount || remaining <= 0);

        btn.interactable = canGo;
    }



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

    // 指定IDのカードを捨て札から1枚だけ削除（同期用）
    public void RemoveFromDiscardById(int cardId)
    {
        var toRemove = discardDataList.FirstOrDefault(d => d != null && d.id == cardId);
        if (toRemove != null)
        {
            discardDataList.Remove(toRemove);
            if (isOpen) BuildFullView();
        }
    }

    // 捨て札一覧の開閉
    public void OnDiscardClicked()
    {
        isOpen = !isOpen;
        if (fullViewPanel == null || gridParent == null || cardDisplayPrefab == null) return;

        if (isOpen)
        {
            // ★ 0枚なら「捨て札はありません」と軽く出してすぐ閉じる
            if (discardDataList.Count == 0)
            {
                ShowDiscardWarning("捨て札はありません。", 1f);
                isOpen = false;
                fullViewPanel.SetActive(false);
                ClearFullView();
                return;
            }

            fullViewPanel.SetActive(true);
            BuildFullView();
        }
        else
        {
            fullViewPanel.SetActive(false);
            ClearFullView();
        }
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

            // 回収モード中は「選択済み枚数」を差し引いて表示する（discardDataList自体は変更しない）
            if (isRecoverMode)
            {
                count -= GetSelectedCountById(g.Key);
                if (count <= 0) continue;
            }

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
    // 回収モード開始
    public void StartRecoverMode(PlayerManager player, int count)
    {
        isRecoverComplete = false; // ★回収開始時にリセット
        isRecoverMode = true;

        // ★変更：捨て札が少ない場合は「全回収」扱いに寄せる（詰み防止）
        int total = discardDataList.Count(d => d != null);
        recoverCount = Mathf.Clamp(count, 0, total);

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

        // ★追加：0なら選びようがないので即終了（保険）
        if (recoverCount <= 0)
        {
            isRecoverComplete = true;
            EndRecoverMode();
            return;
        }

        fullViewPanel.SetActive(true);
        BuildFullView();
        BuildRecoverZone();

        // ★追加：開始時点のボタン状態
        UpdateCompleteButton();
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
    // 捨て札 → 回収
    public void MoveCardToRecover(CardGenerator.CardData data)
    {
        if (data == null) return;

        // ★追加：回収上限に達していたらこれ以上選ばせない
        if (recoverCount > 0 && selectedCards.Count >= recoverCount)
        {
            ShowDiscardWarning($"指定枚数（{recoverCount}枚）までです。", 1f);
            return;
        }

        int selectedCount = GetSelectedCountById(data.id);
        int totalCount = discardDataList.Count(d => d != null && d.id == data.id);

        if (totalCount - selectedCount <= 0)
        {
            // これ以上選べない（同IDを全部選択済み）
            return;
        }

        var any = discardDataList.FirstOrDefault(d => d != null && d.id == data.id);
        if (any != null)
        {
            selectedCards.Add(any);
            BuildFullView();
            BuildRecoverZone();

            // ★追加
            UpdateCompleteButton();
        }
    }



    // 回収 → 捨て札
    // 回収 → 捨て札
    public void MoveCardBackToDiscard(CardGenerator.CardData data)
    {
        if (data == null) return;

        var toRemove = selectedCards.FirstOrDefault(d => d != null && d.id == data.id);
        if (toRemove != null)
        {
            selectedCards.Remove(toRemove);

            BuildFullView();
            BuildRecoverZone();

            // ★追加
            UpdateCompleteButton();
        }
    }



    // 完了ボタン
    // 完了ボタン
    public void OnCompleteButton()
    {
        if (confirmPanel == null) return;

        int total = discardDataList.Count(d => d != null);
        int selected = selectedCards.Count(d => d != null);
        int remaining = total - selected;

        // ★追加：押せる条件（ぴったり or 捨て札残りなし）以外は確認に進めない
        bool canGo = (recoverCount > 0) && (selected > 0) && (selected == recoverCount || remaining <= 0);
        if (!canGo)
        {
            // まだ捨て札が残ってるなら「ぴったり選んで」
            if (selected < recoverCount && remaining > 0)
                ShowDiscardWarning($"指定枚数（{recoverCount}枚）ぴったり選んでください。", 1f);
            else
                ShowDiscardWarning("回収するカードを選んでください。", 1f);

            return;
        }

        // （以下は元のまま）確認画面へ
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
            confirmMessage.text = $"これを回収しますか？（{selected}/{recoverCount}）";
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
        if (recoverTargetPlayer == null || recoverTargetPlayer.Object == null)
        {
            Debug.LogError("OnConfirmOK: recoverTargetPlayer が null です。");
            EndRecoverMode();
            return;
        }

        var gm = FindAnyObjectByType<GameManager>();
        if (gm == null)
        {
            Debug.LogError("OnConfirmOK: GameManager が見つかりません。");
            EndRecoverMode();
            return;
        }

        // 選択カードIDをHostへ送る（Hostが捨て札から削除し、手札へ追加して全員へ同期）
        int[] recoverIds = selectedCards
            .Where(d => d != null)
            .Select(d => d.id)
            .ToArray();

        gm.RPC_RequestRecoverDiscard(recoverTargetPlayer.Object.InputAuthority, recoverIds);

        isRecoverComplete = true;
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
        EffectProcessWindow.Instance.ContinueProcess();
    }
}
