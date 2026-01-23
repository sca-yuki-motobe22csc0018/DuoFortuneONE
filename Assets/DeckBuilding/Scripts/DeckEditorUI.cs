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

        // 🔍 入力時リアルタイム検索
        nameSearchField.onValueChanged.AddListener(_ => RefreshCardList());
        minCostDropdown.onValueChanged.AddListener(_ => RefreshCardList());
        maxCostDropdown.onValueChanged.AddListener(_ => RefreshCardList());
        typeDropdown.onValueChanged.AddListener(_ => RefreshCardList());

        // 🔑 Enterキーで検索
        nameSearchField.onSubmit.AddListener(_ => RefreshCardList());
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
    // 表示用タイプ → CSV用コード変換
    //-------------------------------------------------------
    string ConvertTypeLabelToCode(string label)
    {
        switch (label)
        {
            case "Attack": return "A";
            case "Block": return "B";
            case "Defense": return "D";
            case "EX": return "E";
            default: return null; // All
        }
    }

    //-------------------------------------------------------
    // 右側：カード一覧（検索）
    //-------------------------------------------------------
    void RefreshCardList()
    {
        foreach (Transform t in cardListParent)
            Destroy(t.gameObject);

        string keyword = nameSearchField.text;

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
        string typeLabel = typeDropdown.options[typeDropdown.value].text;
        string typeCode = ConvertTypeLabelToCode(typeLabel);
        bool useTypeFilter = !string.IsNullOrEmpty(typeCode);

        foreach (var card in CardDatabase.Instance.cards)
        {
            // -----------------------------
            // 名前 or ふりがな検索
            // -----------------------------
            if (!string.IsNullOrEmpty(keyword))
            {
                bool matchName = card.name.Contains(keyword);

                // ※ card.ruby は実際の変数名に合わせて変更
                bool matchRuby = !string.IsNullOrEmpty(card.ruby) &&
                                 card.ruby.Contains(keyword);

                if (!matchName && !matchRuby)
                    continue;
            }

            if (card.cost < minCost) continue;
            if (card.cost > maxCost) continue;
            if (useTypeFilter && card.type != typeCode) continue;

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
    // 追加 / 削除（省略なし）
    //-------------------------------------------------------
    public void AddCardToDeck(CardInfo card)
    {
        if (currentDeck.Count >= 30)
        {
            deckCountText.text = "デッキは30枚までです";
            return;
        }

        int sameCount = currentDeck.FindAll(x => x == card.number).Count;
        if (sameCount >= 2)
        {
            deckCountText.text = "同じカードは2枚までです";
            return;
        }

        if (card.type == "E")
        {
            foreach (var num in currentDeck)
            {
                var info = CardDatabase.Instance.GetCard(num);
                if (info.type == "E" && info.number != card.number)
                {
                    deckCountText.text = "ほかのType Eのカードは入れられません";
                    return;
                }
            }
        }

        currentDeck.Add(card.number);
        RefreshDeckDisplay();
    }

    public void RemoveCardFromDeck(CardInfo card)
    {
        currentDeck.Remove(card.number);
        RefreshDeckDisplay();
    }

    //-------------------------------------------------------
    // 保存 / リセット / 戻る
    //-------------------------------------------------------
    public void OnSaveButton()
    {
        int deckIndex = deckSelectDropdown.value;

        if (currentDeck.Count != 30)
        {
            deckCountText.text = "デッキ枚数が30ではありません";
            return;
        }

        DeckSaveManager.Instance.SetDeck(deckIndex,
            new DeckData { cardNumbers = new List<string>(currentDeck) });

        deckCountText.text = $"デッキ{deckIndex + 1}をSAVEしました";
    }

    public void OnResetButton()
    {
        int deckIndex = deckSelectDropdown.value;
        currentDeck.Clear();
        DeckSaveManager.Instance.ClearDeck(deckIndex);
        RefreshDeckDisplay();
    }

    public void OnCloseButton()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene("Title");
    }
}
