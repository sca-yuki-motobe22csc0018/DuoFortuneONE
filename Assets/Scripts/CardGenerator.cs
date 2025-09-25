using System.Collections.Generic;
using System.Text;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class CardGenerator : MonoBehaviour, IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("UI Components")]
    public SpriteRenderer imageSpriteRenderer;
    public SpriteRenderer typeSpriteRenderer;
    public Text costText;
    public Text nameText;
    public Text textText;

    [Header("Sorting")]
    public int baseSortingOrder = 0;

    [Header("Card Data")]
    public int cardID;
    public List<CardData> cardList = new List<CardData>();
    public Dictionary<int, CardData> cardDict = new Dictionary<int, CardData>();
    private CardData myData;  // ★ 現在のカード情報を保持する

    [System.Serializable]
    public class CardData
    {
        public int id;
        public string name;
        public string type;
        public string rarity;
        public int cost;
        public string text;
        public string image;

        public string effectType1;
        public string effectType2;
        public string effectType3;
        public string effectType4;
        public string effectType5;
        public string effectType6;

        public string effectValue1;
        public string effectValue2;
        public string effectValue3;
        public string effectValue4;
        public string effectValue5;
        public string effectValue6;
    }

    [HideInInspector] public PlayerManager player;
    public DiscardManager discardManager;

    [Header("Target Area")]
    public Transform targetArea;
    public float targetRadius = 1.0f;

    // ドラッグ管理
    private Camera mainCam;
    private Vector3 offset;
    private bool isDragging = false;

    // 復帰用
    private Transform originalParent;
    private Vector3 originalLocalPos;
    private Quaternion originalLocalRot;
    private Vector3 originalLocalScale;

    // DragRoot
    private Transform dragRoot;

    void Start()
    {
        mainCam = Camera.main;

        if (player == null) player = FindAnyObjectByType<PlayerManager>();
        if (discardManager == null) discardManager = FindAnyObjectByType<DiscardManager>();

        LoadCSV();

        if (cardID != 0 && cardDict.TryGetValue(cardID, out CardData data))
        {
            ApplyCardData(data);
        }

        SetChildSortingOrders();

        if (targetArea == null)
        {
            GameObject t = GameObject.Find("PlayArea") ?? GameObject.FindWithTag("PlayArea");
            if (t != null) targetArea = t.transform;
        }

        GameObject dr = GameObject.Find("DragRoot");
        if (dr == null) dr = new GameObject("DragRoot");
        dragRoot = dr.transform;
    }

    public void ApplyCardData(CardData data)
    {
        myData = data;  // ★ 必ず保存して捨て札に渡せるようにする

        if (costText != null) costText.text = data.cost.ToString();
        if (nameText != null) nameText.text = data.name;
        if (textText != null) textText.text = data.text;

        if (imageSpriteRenderer != null)
        {
            Sprite imageSprite = Resources.Load<Sprite>("CardImages/" + data.image);
            if (imageSprite != null) imageSpriteRenderer.sprite = imageSprite;
        }

        if (typeSpriteRenderer != null)
        {
            Sprite typeSprite = Resources.Load<Sprite>("CardTypes/Card_Type_" + data.type);
            if (typeSprite != null) typeSpriteRenderer.sprite = typeSprite;
        }

        this.name = data.name;
    }

    public CardData GetCardData()
    {
        return myData;
    }

    public void SetChildSortingOrders()
    {
        foreach (Transform child in transform)
        {
            int offsetOrder = 0;
            SortingOffset so = child.GetComponent<SortingOffset>();
            if (so != null) offsetOrder = so.orderOffset;

            SpriteRenderer sr = child.GetComponent<SpriteRenderer>();
            if (sr != null) sr.sortingOrder = baseSortingOrder + offsetOrder;

            Canvas canvas = child.GetComponent<Canvas>();
            if (canvas != null)
            {
                canvas.overrideSorting = true;
                canvas.sortingOrder = baseSortingOrder + offsetOrder;
            }
        }
    }

    void LoadCSV()
    {
        string path = Application.streamingAssetsPath + "/Card_Data.csv";
        if (!File.Exists(path)) return;

        string csvText = File.ReadAllText(path, Encoding.UTF8);
        List<string[]> rows = ParseCsv(csvText);

        cardList.Clear();
        cardDict.Clear();

        for (int i = 1; i < rows.Count; i++)
        {
            string[] values = rows[i];
            if (values.Length < 9) continue;
            if (!int.TryParse(values[0], out int id)) continue;

            CardData data = new CardData
            {
                id = id,
                name = values[1],
                type = values[2],
                rarity = values[3],
                cost = int.TryParse(values[4], out int c) ? c : 0,
                text = values[5],
                image = values[6],

                effectType1 = values.Length > 7 ? values[7] : "",
                effectValue1 = values.Length > 8 ? values[8] : "0",
                effectType2 = values.Length > 9 ? values[9] : "",
                effectValue2 = values.Length > 10 ? values[10] : "0",
                effectType3 = values.Length > 11 ? values[11] : "",
                effectValue3 = values.Length > 12 ? values[12] : "0",
                effectType4 = values.Length > 13 ? values[13] : "",
                effectValue4 = values.Length > 14 ? values[14] : "0",
                effectType5 = values.Length > 15 ? values[15] : "",
                effectValue5 = values.Length > 16 ? values[16] : "0",
                effectType6 = values.Length > 17 ? values[17] : "",
                effectValue6 = values.Length > 18 ? values[18] : "0",
            };

            cardList.Add(data);
            if (!cardDict.ContainsKey(data.id)) cardDict.Add(data.id, data);
        }
    }

    List<string[]> ParseCsv(string csvText)
    {
        var rows = new List<string[]>();
        var currentRow = new List<string>();
        var currentField = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < csvText.Length; i++)
        {
            char c = csvText[i];
            char next = i + 1 < csvText.Length ? csvText[i + 1] : '\0';

            if (inQuotes)
            {
                if (c == '"' && next == '"') { currentField.Append('"'); i++; }
                else if (c == '"') inQuotes = false;
                else currentField.Append(c);
            }
            else
            {
                if (c == '"') inQuotes = true;
                else if (c == ',') { currentRow.Add(currentField.ToString()); currentField.Clear(); }
                else if (c == '\r' && next == '\n')
                {
                    currentRow.Add(currentField.ToString());
                    rows.Add(currentRow.ToArray());
                    currentRow = new List<string>();
                    currentField.Clear();
                    i++;
                }
                else if (c == '\n' || c == '\r')
                {
                    currentRow.Add(currentField.ToString());
                    rows.Add(currentRow.ToArray());
                    currentRow = new List<string>();
                    currentField.Clear();
                }
                else currentField.Append(c);
            }
        }

        if (currentField.Length > 0 || currentRow.Count > 0)
        {
            currentRow.Add(currentField.ToString());
            rows.Add(currentRow.ToArray());
        }

        return rows;
    }

    // =============================
    // ドラッグ関連
    // =============================
    public void OnPointerDown(PointerEventData eventData)
    {
        if (mainCam == null) mainCam = Camera.main;
        Vector3 worldPos = mainCam.ScreenToWorldPoint(eventData.position);
        worldPos.z = 0;
        offset = transform.position - worldPos;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (player == null)
        {
            Debug.LogWarning("CardGenerator.player が未設定です！");
            return;
        }

        isDragging = true;

        originalParent = transform.parent;
        originalLocalPos = transform.localPosition;
        originalLocalRot = transform.localRotation;
        originalLocalScale = transform.localScale;

        HandManager hand = player.handManager;
        if (hand != null && hand.handCards.Contains(gameObject))
        {
            hand.handCards.Remove(gameObject);
            hand.UpdateCardPositions();
        }

        transform.SetParent(dragRoot, true);
        transform.rotation = Quaternion.identity;
        transform.localRotation = Quaternion.identity;
        baseSortingOrder += 10000;
        SetChildSortingOrders();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging) return;
        if (mainCam == null) mainCam = Camera.main;

        Vector3 worldPos = mainCam.ScreenToWorldPoint(eventData.position);
        worldPos.z = 0;
        transform.position = worldPos + offset;
        transform.rotation = Quaternion.identity;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!isDragging) return;
        isDragging = false;

        Vector3 worldPos = mainCam.ScreenToWorldPoint(eventData.position);
        worldPos.z = 0;

        bool used = false;
        if (targetArea != null)
        {
            Collider2D col = targetArea.GetComponent<Collider2D>();
            if (col != null && col.OverlapPoint(worldPos))
            {
                used = TryPlayCard();
            }
        }

        if (!used)
        {
            RestoreToHand(player);
        }
    }

    void RestoreToHand(PlayerManager targetPlayer)
    {
        if (targetPlayer == null)
        {
            transform.SetParent(originalParent, false);
            transform.localPosition = originalLocalPos;
            transform.localRotation = originalLocalRot;
            transform.localScale = originalLocalScale;
        }
        else
        {
            HandManager hand = targetPlayer.handManager;
            if (hand != null)
            {
                transform.SetParent(hand.transform, false);
                hand.AddCard(gameObject);
                hand.UpdateCardPositions();
            }
        }

        baseSortingOrder -= 10000;
        SetChildSortingOrders();
    }

    public bool TryPlayCard()
    {
        if (player == null || myData == null) return false;
        if (!player.SpendMana(myData.cost)) return false;
        player.UpdateEnergyUI();

        ActivateEffect();

        if (discardManager != null)
        {
            // データだけ捨て札に送る
            discardManager.AddToDiscard(myData);
            Destroy(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }

        return true;
    }

    public void ActivateEffect()
    {
        if (myData == null) return;

        ApplyEffect(myData.effectType1, myData.effectValue1);
        ApplyEffect(myData.effectType2, myData.effectValue2);
        ApplyEffect(myData.effectType3, myData.effectValue3);
        ApplyEffect(myData.effectType4, myData.effectValue4);
        ApplyEffect(myData.effectType5, myData.effectValue5);
        ApplyEffect(myData.effectType6, myData.effectValue6);
    }

    void ApplyEffect(string type, string value)
    {
        if (string.IsNullOrEmpty(type)) return;

        //Debug.Log($"ApplyEffect: {type} ({value})"); // 仮ログ出力（呼ばれた確認用）

        switch (type)
        {
            case "Draw":
                if (int.TryParse(value, out int drawCount))
                    DoDraw(drawCount);
                break;

            case "ManaBoost":
                if (int.TryParse(value, out int boost))
                    DoManaBoost(boost);
                break;

            case "ManaRecover":
                if (int.TryParse(value, out int recover))
                    DoManaRecover(recover);
                break;

            case "ManaReduce":
                Debug.Log($"相手のマナ -{value}");
                break;

            case "ManaReduceMax":
                Debug.Log($"自分の最大マナ -{value}");
                break;

            case "ManaReduceMaxOpponent":
                Debug.Log($"相手の最大マナ -{value}");
                break;

            case "LifeAdd":
                if (int.TryParse(value, out int life))
                    DoLifeAdd(life);
                break;

            case "Attack":
                Debug.Log($"Attack {value} 回");
                break;

            case "Block":
                Debug.Log($"Block {value} 回");
                break;

            case "DiscardSelf":
                Debug.Log($"自分の手札から {value} 枚捨てる");
                break;

            case "DiscardOpponent":
                Debug.Log($"相手の手札から {value} 枚捨てる");
                break;

            case "RecoverDiscard":
                if (int.TryParse(value, out int count))
                    DoRecoverDiscard(count);
                break;

            case "StealHand":
                Debug.Log($"相手の手札から {value} 枚奪う");
                break;

            case "SwapHands":
                Debug.Log("相手と手札を交換");
                break;

            case "Choice":
                Debug.Log($"選択肢から {value} 回選ぶ");
                break;

            case "EndTurn":
                DoEndTurn();
                break;

            case "EndTurnIfMyTurn":
                {
                    var gm = FindAnyObjectByType<GameManager>();
                    if (gm != null && gm.IsMyTurn(player))
                    {
                        DoEndTurn();
                    }
                }
                break;

            case "Defence":
                Debug.Log("Defence 効果発動（複数効果）");
                break;

            default:
                Debug.LogWarning($"未対応の効果: {type}");
                break;
        }
    }

    void DoDraw(int count)
    {
        var deck = FindAnyObjectByType<DeckManager>();
        if (deck == null) return;

        for (int i = 0; i < count; i++)
            deck.DrawCardToHand(player);
    }

    void DoManaBoost(int amount)
    {
        if (player != null)
        {
            player.IncreaseMaxManaOnly(amount);
            player.UpdateEnergyUI(); // ← 即座にUI反映
        }
    }

    void DoManaRecover(int amount)
    {
        if (player != null)
            player.currentMana = Mathf.Min(player.currentMana + amount, player.maxMana);
    }

    void DoLifeAdd(int amount)
    {
        if (player != null && player.lifeManager != null)
        {
            for (int i = 0; i < amount; i++)
                player.lifeManager.AddLife();
        }
    }
    void DoRecoverDiscard(int count)
    {
        var discard = FindAnyObjectByType<DiscardManager>();
        if (discard != null)
        {
            discard.StartRecoverMode(player, count);
        }
    }

    void DoEndTurn()
    {
        var gm = FindAnyObjectByType<GameManager>();
        if (gm != null) gm.OnEndTurn();
    }
}
