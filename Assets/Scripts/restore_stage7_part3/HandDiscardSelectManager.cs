using Fusion;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class HandDiscardSelectManager : MonoBehaviour
{
    [Header("Root Panels")]
    public GameObject selectPanel;     // 選ぶ画面のPanel（全体）
    public GameObject confirmPanel;    // 確認画面のPanel（全体）

    [Header("Select Screen UI")]
    public Transform handListParent;        // 手札一覧のContent
    public Transform selectedListParent;    // 選んだカード一覧のContent
    public GameObject cardDisplayPrefab;    // DiscardUIPanelで使ってる表示Prefab（CardUI付き想定）
    public TMP_Text limitText;              // 「捨てる枚数：N」など
    public TMP_Text warningText;            // 警告表示（任意）
    public GameObject toConfirmButton;      // OKボタン（選ぶ画面）

    [Header("Confirm Screen UI")]
    public Transform confirmListParent;     // 確認画面のContent
    public GameObject confirmCardPrefab;    // 確認用Prefab（cardDisplayPrefabでもOK）
    public TMP_Text confirmMessage;         // 「これでOK？」など
    public GameObject backButton;           // 戻る
    public GameObject confirmOkButton;      // OK（確定）

    private PlayerManager targetPlayer;
    private int discardLimit = 0;           // 選べる枚数（ALLの場合は開始時に手札枚数に置換）
    private bool isSelecting = false;
    private bool isComplete = false;

    // 「選択したカードID（多重あり）」をここに貯める
    private List<int> selectedCardIds = new List<int>();

    // 表示用：手札のCardData一覧（開始時点のスナップショットではなく、都度 handManager から引く）
    // ※ 選択中に手札が動く可能性があるなら「都度引く」の方が安全
    public bool IsComplete => isComplete;

    void Start()
    {
        if (selectPanel != null) selectPanel.SetActive(false);
        if (confirmPanel != null) confirmPanel.SetActive(false);
        if (warningText != null) warningText.gameObject.SetActive(false);
    }

    // =========================
    // 外部（CardGenerator）から呼ぶ入口
    // =========================
    public void StartSelectDiscardMode(PlayerManager player, int limit)
    {
        isComplete = false;
        isSelecting = true;
        targetPlayer = player;
        selectedCardIds.Clear();

        if (targetPlayer == null || targetPlayer.handManager == null)
        {
            Finish();
            return;
        }

        // 自分の画面だけ開く（相手の手札は見せない前提）
        if (targetPlayer.Object == null || !targetPlayer.Object.HasInputAuthority)
        {
            // 自分じゃないならUI出さずに即完了（相手が選ぶべき効果は今回は作らない前提）
            Finish();
            return;
        }

        // ALL扱い：limit < 0 のとき、開始時点の手札枚数にする
        if (limit < 0)
        {
            discardLimit = Mathf.Max(0, targetPlayer.handManager.CardCount);
        }
        else
        {
            discardLimit = Mathf.Max(1, limit);
        }

        if (limitText != null)
            limitText.text = $"捨てる枚数：{discardLimit}";

        if (warningText != null) warningText.gameObject.SetActive(false);

        if (confirmPanel != null) confirmPanel.SetActive(false);
        if (selectPanel != null) selectPanel.SetActive(true);

        BuildHandList();
        BuildSelectedList();
    }

    // =========================
    // UI構築
    // =========================
    private void ClearChildren(Transform parent)
    {
        if (parent == null) return;
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            Destroy(parent.GetChild(i).gameObject);
        }
    }

    private List<CardGenerator.CardData> GetCurrentHandDataList()
    {
        var result = new List<CardGenerator.CardData>();
        if (targetPlayer == null || targetPlayer.handManager == null) return result;

        foreach (var go in targetPlayer.handManager.handCards)
        {
            if (go == null) continue;
            var cg = go.GetComponent<CardGenerator>();
            if (cg == null) continue;

            var data = cg.GetCardData();
            if (data != null) result.Add(data);
        }
        return result;
    }

    private int GetSelectedCountById(int id)
    {
        return selectedCardIds.Count(x => x == id);
    }

    private void BuildHandList()
    {
        ClearChildren(handListParent);

        var hand = GetCurrentHandDataList();
        var grouped = hand
            .Where(d => d != null)
            .GroupBy(d => d.id)
            .OrderBy(g => g.Key);

        foreach (var g in grouped)
        {
            var data = g.First();
            int total = g.Count();

            // 既に選んだ分を差し引いた “残り” を表示
            int remain = total - GetSelectedCountById(g.Key);
            if (remain <= 0) continue;

            GameObject ui = Instantiate(cardDisplayPrefab, handListParent);

            // 表示は CardUI に任せる（DiscardManager同様）
            var cardUI = ui.GetComponent<CardUI>();
            if (cardUI != null)
            {
                // 3引数版がプロジェクトに存在している前提（DiscardManagerのconfirmでも使ってる）
                cardUI.SetCard(data, remain, null);
            }

            var detail = ui.GetComponent<CardUIDetail>();
            if (detail != null) detail.Init(data);

            // クリックで「選択に追加」
            AttachClick(ui, () => OnClickHandCard(data.id));
        }
    }

    private void BuildSelectedList()
    {
        ClearChildren(selectedListParent);

        var hand = GetCurrentHandDataList();
        // id→CardData を引けるように（同IDは同じデータでOK）
        var dict = hand
            .Where(d => d != null)
            .GroupBy(d => d.id)
            .ToDictionary(g => g.Key, g => g.First());

        var grouped = selectedCardIds
            .GroupBy(id => id)
            .OrderBy(g => g.Key);

        foreach (var g in grouped)
        {
            int id = g.Key;
            int count = g.Count();
            if (!dict.TryGetValue(id, out var data) || data == null) continue;

            GameObject ui = Instantiate(cardDisplayPrefab, selectedListParent);

            var cardUI = ui.GetComponent<CardUI>();
            if (cardUI != null)
            {
                cardUI.SetCard(data, count, null);
            }

            var detail = ui.GetComponent<CardUIDetail>();
            if (detail != null) detail.Init(data);

            // クリックで「選択から戻す（1枚分だけ）」
            AttachClick(ui, () => OnClickSelectedCard(id));
        }
    }

    private void BuildConfirmList()
    {
        ClearChildren(confirmListParent);

        var hand = GetCurrentHandDataList();
        var dict = hand
            .Where(d => d != null)
            .GroupBy(d => d.id)
            .ToDictionary(g => g.Key, g => g.First());

        var grouped = selectedCardIds
            .GroupBy(id => id)
            .OrderBy(g => g.Key);

        foreach (var g in grouped)
        {
            int id = g.Key;
            int count = g.Count();
            if (!dict.TryGetValue(id, out var data) || data == null) continue;

            GameObject ui = Instantiate(confirmCardPrefab != null ? confirmCardPrefab : cardDisplayPrefab, confirmListParent);

            var cardUI = ui.GetComponent<CardUI>();
            if (cardUI != null)
            {
                cardUI.SetCard(data, count, null);
            }

            var detail = ui.GetComponent<CardUIDetail>();
            if (detail != null) detail.Init(data);
        }

        if (confirmMessage != null)
        {
            if (selectedCardIds.Count < discardLimit)
                confirmMessage.text = $"まだ選べますが大丈夫ですか？（{selectedCardIds.Count}/{discardLimit}）";
            else
                confirmMessage.text = $"これを捨てますか？（{selectedCardIds.Count}/{discardLimit}）";
        }
    }

    // =========================
    // クリック処理
    // =========================
    public void OnClickHandCard(int cardId)
    {
        if (!isSelecting) return;

        if (selectedCardIds.Count >= discardLimit)
        {
            ShowWarning($"指定枚数（{discardLimit}枚）までです。");
            return;
        }

        selectedCardIds.Add(cardId);
        BuildHandList();
        BuildSelectedList();
    }

    private void OnClickSelectedCard(int cardId)
    {
        if (!isSelecting) return;

        int idx = selectedCardIds.FindIndex(x => x == cardId);
        if (idx >= 0)
        {
            selectedCardIds.RemoveAt(idx);
            BuildHandList();
            BuildSelectedList();
        }
    }

    private void ShowWarning(string msg, float sec = 1f)
    {
        if (warningText == null) return;

        warningText.text = msg;
        warningText.gameObject.SetActive(true);
        StopAllCoroutines();
        StartCoroutine(CoHideWarning(sec));
    }

    private IEnumerator CoHideWarning(float sec)
    {
        yield return new WaitForSeconds(sec);
        if (warningText != null) warningText.gameObject.SetActive(false);
    }

    // =========================
    // ボタン（選ぶ画面OK → 確認画面）
    // =========================
    public void OnToConfirm()
    {
        if (!isSelecting) return;

        if (selectedCardIds.Count == 0)
        {
            ShowWarning("捨てるカードを選んでください。");
            return;
        }

        // 多すぎる場合は止める（保険）
        if (selectedCardIds.Count > discardLimit)
        {
            ShowWarning($"指定枚数（{discardLimit}枚）より多く選択しています！", 1f);
            return;
        }

        BuildConfirmList();
        if (confirmPanel != null) confirmPanel.SetActive(true);
    }

    public void OnBackFromConfirm()
    {
        if (confirmPanel != null) confirmPanel.SetActive(false);
    }

    // =========================
    // 確認OK（確定 → Hostへ依頼）
    // =========================
    public void OnConfirmOk()
    {
        if (!isSelecting) return;

        var gm = GameManager.Instance ?? FindAnyObjectByType<GameManager>();
        var runner = FindAnyObjectByType<NetworkRunner>();

        if (gm == null || runner == null || targetPlayer == null || targetPlayer.Object == null)
        {
            Finish();
            return;
        }

        // 自分の手札を捨てる（今回の仕様）
        PlayerRef requester = runner.LocalPlayer;
        PlayerRef targetRef = targetPlayer.Object.InputAuthority;

        gm.RPC_RequestDiscardFromHand(requester, targetRef, selectedCardIds.ToArray());

        Finish();
    }

    private void Finish()
    {
        isSelecting = false;
        isComplete = true;

        if (confirmPanel != null) confirmPanel.SetActive(false);
        if (selectPanel != null) selectPanel.SetActive(false);
        ClearChildren(handListParent);
        ClearChildren(selectedListParent);
        ClearChildren(confirmListParent);
    }

    // =========================
    // “Prefabに手を入れず” クリックを付けるための共通処理
    // =========================
    private void AttachClick(GameObject uiRoot, Action onClick)
    {
        if (uiRoot == null) return;

        // 既にあればそれを使う
        var clicker = uiRoot.GetComponent<SimpleUIPointerClick>();
        if (clicker == null) clicker = uiRoot.AddComponent<SimpleUIPointerClick>();

        clicker.onClick = onClick;
    }

    private class SimpleUIPointerClick : MonoBehaviour, IPointerClickHandler
    {
        public Action onClick;

        public void OnPointerClick(PointerEventData eventData)
        {
            onClick?.Invoke();
        }
    }
}
