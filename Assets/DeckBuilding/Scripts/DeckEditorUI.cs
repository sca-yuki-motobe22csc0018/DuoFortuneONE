using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

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
    [SerializeField] Image deckFade;

    [Header("SE")]
    public AudioSource seSource;
    public AudioClip seCenter; // センター用
    public AudioClip seRight;  // ライト用


    void Start()
    {
        LoadDeckFromSave();
        RefreshCardList();
        RefreshDeckDisplay();
        deckFade.raycastTarget = false;
        deckFade.DOFade(0f, 1.0f);
        // デッキ切り替え
        deckSelectDropdown.onValueChanged.AddListener(_ => OnDeckChanged());

        // 🔍 入力時リアルタイム検索
        nameSearchField.onValueChanged.AddListener(_ => RefreshCardList());
        minCostDropdown.onValueChanged.AddListener(_ => RefreshCardList());
        maxCostDropdown.onValueChanged.AddListener(_ => RefreshCardList());
        typeDropdown.onValueChanged.AddListener(_ => RefreshCardList());

        // 🔑 Enterキーで検索（入力保持）
        nameSearchField.onSubmit.AddListener(_ =>
        {
            RefreshCardList();
            nameSearchField.ActivateInputField();
        });
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

        // 🔍 検索キーワード正規化
        string keyword = nameSearchField.text.Trim().ToLower();

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
                string cardName = card.name?.ToLower() ?? "";
                string cardRuby = card.ruby?.ToLower() ?? "";

                bool match =
                    cardName.Contains(keyword) ||
                    cardRuby.Contains(keyword);

                if (!match)
                    continue;
            }

            // コスト
            if (card.cost < minCost) continue;
            if (card.cost > maxCost) continue;

            // タイプ
            if (useTypeFilter && card.type != typeCode)
                continue;

            var obj = Instantiate(listItemPrefab, cardListParent);
            obj.GetComponent<CardDisplayImageOnly>().SetCard(card, this);

            Button btn = obj.GetComponent<Button>();
            btn.onClick.AddListener(() =>
            {
                PlayRightSE();          // ★ ライト用SE
                AddCardToDeck(card);
                ShowDetail(card);
            });
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
            btn.onClick.AddListener(() =>
            {
                PlayCenterSE();         // ★ センター用SE
                RemoveCardFromDeck(info);
            });
        }

        deckCountText.text = $"現在のデッキ枚数 {currentDeck.Count}/30";
    }

    //-------------------------------------------------------
    // 追加 / 削除
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

        // Type E 制限
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

        DeckSaveManager.Instance.SetDeck(
            deckIndex,
            new DeckData { cardNumbers = new List<string>(currentDeck) }
        );

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
        deckFade.raycastTarget = true;//2/5追加
        deckFade.DOFade(1f, 1.0f).OnComplete(() =>
        {
            UnityEngine.SceneManagement.SceneManager.LoadScene("Title");
        });
    }

    public void PlayCenterSE()
    {
        if (seSource && seCenter)
            seSource.PlayOneShot(seCenter);
    }

    public void PlayRightSE()
    {
        if (seSource && seRight)
            seSource.PlayOneShot(seRight);
    }

}
