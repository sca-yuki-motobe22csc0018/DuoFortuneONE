using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class DeckEditorUI : MonoBehaviour
{
    [Header("UI References")]
    public Transform cardListParent;
    public Transform deckParent;

    public GameObject listItemPrefab;
    public GameObject deckItemPrefab;

    public CardDetailUI cardDetailUI;

    [Header("Deck Status")]
    public TMP_Text deckCountText;

    [Header("Search UI")]
    public TMP_InputField nameSearchField;
    public TMP_Dropdown minCostDropdown;
    public TMP_Dropdown maxCostDropdown;
    public TMP_Dropdown typeDropdown;

    [Header("Deck Select")]
    public TMP_Dropdown deckSelectDropdown;

    List<string> currentDeck = new();

    void Start()
    {
        LoadDeckFromSave();
        RefreshCardList();
        RefreshDeckDisplay();

        // デッキ切り替え
        deckSelectDropdown.onValueChanged.AddListener(_ => OnDeckChanged());

        // 🔍 検索条件変更で即更新
        nameSearchField.onValueChanged.AddListener(_ => RefreshCardList());
        minCostDropdown.onValueChanged.AddListener(_ => RefreshCardList());
        maxCostDropdown.onValueChanged.AddListener(_ => RefreshCardList());
        typeDropdown.onValueChanged.AddListener(_ => RefreshCardList());
    }

    //-------------------------------------------------------
    // デッキ切り替え
    //-------------------------------------------------------
    void OnDeckChanged()
    {
        LoadDeckFromSave();
        RefreshDeckDisplay();
    }

    //-------------------------------------------------------
    // デッキ読み込み
    //-------------------------------------------------------
    void LoadDeckFromSave()
    {
        int deckIndex = deckSelectDropdown.value;
        var deck = DeckSaveManager.Instance.GetDeck(deckIndex);

        if (deck == null || deck.cardNumbers == null)
            currentDeck = new List<string>();
        else
            currentDeck = new List<string>(deck.cardNumbers);
    }

    //-------------------------------------------------------
    // 右側：カード一覧（検索）
    //-------------------------------------------------------
    void RefreshCardList()
    {
        foreach (Transform t in cardListParent)
            Destroy(t.gameObject);

        string nameKeyword = nameSearchField.text;

        // --- 最小コスト ---
        int minCost = int.MinValue;
        string minText = minCostDropdown.options[minCostDropdown.value].text;
        if (minText != "Any")
            int.TryParse(minText, out minCost);

        // --- 最大コスト ---
        int maxCost = int.MaxValue;
        string maxText = maxCostDropdown.options[maxCostDropdown.value].text;
        if (maxText != "Any")
            int.TryParse(maxText, out maxCost);

        // --- タイプ ---
        string selectedType = typeDropdown.options[typeDropdown.value].text;
        bool useTypeFilter = selectedType != "All";

        foreach (var card in CardDatabase.Instance.cards)
        {
            // 名前
            if (!string.IsNullOrEmpty(nameKeyword) &&
                !card.name.Contains(nameKeyword))
                continue;

            // 最小コスト
            if (card.cost < minCost)
                continue;

            // 最大コスト
            if (card.cost > maxCost)
                continue;

            // タイプ
            if (useTypeFilter && card.type != selectedType)
                continue;

            var obj = Instantiate(listItemPrefab, cardListParent);
            obj.GetComponent<CardDisplayImageOnly>().SetCard(card, this);

            Button btn = obj.GetComponent<Button>();
            btn.onClick.AddListener(() => AddCardToDeck(card));
            btn.onClick.AddListener(() => ShowDetail(card));
        }
    }

    //-------------------------------------------------------
    // 左側：カード詳細
    //-------------------------------------------------------
    public void ShowDetail(CardInfo card)
    {
        cardDetailUI.Show(card);
    }

    //-------------------------------------------------------
    // 中央：デッキ表示
    //-------------------------------------------------------
    void RefreshDeckDisplay()
    {
        foreach (Transform t in deckParent)
            Destroy(t.gameObject);

        foreach (var num in currentDeck)
        {
            var info = CardDatabase.Instance.GetCard(num);
            if (info == null) continue;

            var obj = Instantiate(deckItemPrefab, deckParent);
            obj.GetComponent<CardDisplayImageOnly>().SetCard(info, this);

            Button btn = obj.GetComponent<Button>();
            btn.onClick.AddListener(() => RemoveCardFromDeck(info));
        }

        deckCountText.text = $"現在のデッキ枚数 {currentDeck.Count}/30";
    }

    //-------------------------------------------------------
    // 追加 / 削除
    //-------------------------------------------------------
    public void AddCardToDeck(CardInfo card)
    {
        // 30枚制限
        if (currentDeck.Count >= 30)
        {
            deckCountText.text = "デッキは30枚までです";
            return;
        }

        // 同名2枚制限（全カード共通）
        int sameCardCount = currentDeck.FindAll(x => x == card.number).Count;
        if (sameCardCount >= 2)
        {
            deckCountText.text = "同じカードは2枚までです";
            return;
        }

        // -----------------------------
        // Type E 特別ルール
        // -----------------------------
        if (card.type == "E")
        {
            foreach (var num in currentDeck)
            {
                var info = CardDatabase.Instance.GetCard(num);
                if (info == null) continue;

                // すでに別種類の Type E が入っている
                if (info.type == "E" && info.number != card.number)
                {
                    deckCountText.text = "ほかのType E のカードは入れられません";
                    return;
                }
            }
        }

        // 追加成功
        currentDeck.Add(card.number);
        RefreshDeckDisplay();

        deckCountText.text = $"現在のデッキ枚数 {currentDeck.Count}/30";
    }



    public void RemoveCardFromDeck(CardInfo card)
    {
        currentDeck.Remove(card.number);
        RefreshDeckDisplay();
    }

    //-------------------------------------------------------
    // 保存
    //-------------------------------------------------------
    public void OnSaveButton()
    {
        int deckIndex = deckSelectDropdown.value;

        if (currentDeck.Count != 30)
        {
            deckCountText.text = "デッキ枚数が30ではありません";
            return;
        }

        DeckData data = new DeckData
        {
            cardNumbers = new List<string>(currentDeck)
        };

        DeckSaveManager.Instance.SetDeck(deckIndex, data);

        deckCountText.text = $"デッキ{deckIndex + 1}をSAVEしました";
    }

    //-------------------------------------------------------
    // リセット
    //-------------------------------------------------------
    public void OnResetButton()
    {
        currentDeck.Clear();
        int deckIndex = deckSelectDropdown.value;
        DeckSaveManager.Instance.ClearDeck(deckIndex);
        LoadDeckFromSave();
        RefreshDeckDisplay();
        deckCountText.text = $"デッキ{deckIndex + 1}をResetしました";
    }

    //-------------------------------------------------------
    // 戻る
    //-------------------------------------------------------
    public void OnCloseButton()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene("Title");
    }
}
