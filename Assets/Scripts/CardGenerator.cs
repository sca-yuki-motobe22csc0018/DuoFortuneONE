using System.Collections.Generic;
using System.Text;
using System.IO;
using UnityEngine;
using TMPro;
using UnityEngine.EventSystems;

public class CardGenerator : MonoBehaviour, IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("UI Components")]
    public SpriteRenderer imageSpriteRenderer;
    public SpriteRenderer typeSpriteRenderer;
    public TMP_Text costText;
    public TMP_Text nameText;
    public TMP_Text textText;

    [Header("Sorting")]
    public int baseSortingOrder = 0;

    [Header("Card Data")]
    public int cardID;
    public List<CardData> cardList = new List<CardData>();
    public Dictionary<int, CardData> cardDict = new Dictionary<int, CardData>();
    private CardData myData;

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

    private Camera mainCam;
    private Vector3 offset;
    private bool isDragging = false;

    private Transform originalParent;
    private Vector3 originalLocalPos;
    private Quaternion originalLocalRot;
    private Vector3 originalLocalScale;

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
        myData = data;

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

    // 仮メソッド (元の実装に依存)
    private bool TryPlayCard()
    {
        Debug.Log("カードをプレイ！");
        return true;
    }

    private void RestoreToHand(PlayerManager p)
    {
        transform.SetParent(originalParent);
        transform.localPosition = originalLocalPos;
        transform.localRotation = originalLocalRot;
        transform.localScale = originalLocalScale;
        baseSortingOrder -= 10000;
        SetChildSortingOrders();

        HandManager hand = p.handManager;
        if (hand != null && !hand.handCards.Contains(gameObject))
        {
            hand.handCards.Add(gameObject);
            hand.UpdateCardPositions();
        }
    }
}
