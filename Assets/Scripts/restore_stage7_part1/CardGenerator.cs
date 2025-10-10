using System.Collections.Generic;
using System.Text;
using System.IO;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections; // ★ 追加（コルーチン用）

public class CardGenerator : MonoBehaviour, IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("UI Components")]
    public Image cardImage;
    public Image typeImage;
    public TMP_Text costText;
    public TMP_Text nameText;
    public TMP_Text rubyText;
    public TMP_Text textText;

    [Header("Sorting")]
    public int baseSortingOrder = 0;

    [Header("Card Data")]
    public int cardID;
    public List<CardData> cardList = new List<CardData>();
    public Dictionary<int, CardData> cardDict = new Dictionary<int, CardData>();
    private CardData myData;
    // ★ 外部から参照できる読み取り専用プロパティ
    public CardData cardData => myData;

    [System.Serializable]
    public class CardData
    {
        public int id;
        public string name;
        public string ruby;
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

        // --- テキスト設定 ---
        if (costText != null)
            costText.text = data.cost.ToString();

        if (nameText != null)
            nameText.text = data.name;

        if (rubyText != null)
            rubyText.text = data.ruby;

        if (textText != null)
            textText.text = data.text;

        // --- カード画像 ---
        if (cardImage != null)
        {
            var imageSprite = Resources.Load<Sprite>("CardImages/" + data.image);
            if (imageSprite)
                cardImage.sprite = imageSprite;
            else
                Debug.LogWarning($"Card image not found: CardImages/{data.image}");
        }

        // --- タイプ画像 ---
        if (typeImage != null)
        {
            var typeSprite = Resources.Load<Sprite>("CardTypes/Card_Type_" + data.type);
            if (typeSprite)
                typeImage.sprite = typeSprite;
            else
                Debug.LogWarning($"Type image not found: CardTypes/Card_Type_{data.type}");
        }

        // --- 任意でタイプ文字も設定できるように ---
        // もしUI上にTMP_Textを後で追加する場合に備えて
        var tmp = GetComponentInChildren<TMP_Text>(true);
        if (tmp != null && tmp.gameObject.name.Contains("TypeText"))
        {
            string typeLabel = data.type switch
            {
                "A" => "ATTACK",
                "B" => "BLOCK",
                "D" => "DEFENCE",
                "E" => "EX",
                _ => data.type
            };
            tmp.text = typeLabel;
        }

        // --- オブジェクト名更新 ---
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
                ruby = values[2],
                type = values[3],
                rarity = values[4],
                cost = int.TryParse(values[5], out int c) ? c : 0,
                text = values[6],
                image = values[7],

                effectType1 = values.Length > 8 ? values[8] : "",
                effectValue1 = values.Length > 9 ? values[9] : "0",
                effectType2 = values.Length > 10 ? values[10] : "",
                effectValue2 = values.Length > 11 ? values[11] : "0",
                effectType3 = values.Length > 12 ? values[12] : "",
                effectValue3 = values.Length > 13 ? values[13] : "0",
                effectType4 = values.Length > 14 ? values[14] : "",
                effectValue4 = values.Length > 15 ? values[15] : "0",
                effectType5 = values.Length > 16 ? values[16] : "",
                effectValue5 = values.Length > 17 ? values[17] : "0",
                effectType6 = values.Length > 18 ? values[18] : "",
                effectValue6 = values.Length > 19 ? values[19] : "0",
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

    // ==============================
    // ドラッグ関連
    // ==============================
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

    // ===================================
    // ▼ 効果処理（順次実行＋手動進行対応）
    // ===================================
    public bool TryPlayCard()
    {
        if (player == null || myData == null) return false;
        if (!player.SpendMana(myData.cost)) return false;
        player.UpdateEnergyUI();

        // ★ 修正: 使用確定時、GameObjectは非アクティブ化せず"見た目だけ"即座に消す
        HideVisualsForUsing();

        StartCoroutine(EffectSequenceCoroutine()); // ★ コルーチン開始
        return true;
    }

    /// <summary>
    /// 見た目とクリック判定だけを無効化して、コルーチンは継続させる
    /// </summary>
    private void HideVisualsForUsing()
    {
        // UI(Image, TMP) を不可視化 & Raycast無効
        var images = GetComponentsInChildren<Image>(true);
        foreach (var img in images) { img.raycastTarget = false; img.enabled = false; }

        var texts = GetComponentsInChildren<TMP_Text>(true);
        foreach (var t in texts) { t.raycastTarget = false; t.enabled = false; }

        // 2D/3Dレンダラーを不可視化
        var srs = GetComponentsInChildren<SpriteRenderer>(true);
        foreach (var sr in srs) sr.enabled = false;

        // 当たり判定オフ（誤クリック防止）
        var cols2D = GetComponentsInChildren<Collider2D>(true);
        foreach (var c in cols2D) c.enabled = false;

        var cols3D = GetComponentsInChildren<Collider>(true);
        foreach (var c in cols3D) c.enabled = false;
    }

    private IEnumerator EffectSequenceCoroutine()
    {
        var processWindow = FindAnyObjectByType<EffectProcessWindow>();

        var effects = new List<(string type, string value)>()
        {
            (myData.effectType1, myData.effectValue1),
            (myData.effectType2, myData.effectValue2),
            (myData.effectType3, myData.effectValue3),
            (myData.effectType4, myData.effectValue4),
            (myData.effectType5, myData.effectValue5),
            (myData.effectType6, myData.effectValue6),
        };

        foreach (var e in effects)
        {
            if (string.IsNullOrEmpty(e.type)) continue;

            bool isAuto = IsAutoEffect(e.type);

            if (processWindow != null)
                processWindow.ShowMessage($"効果実行中: {e.type} ({e.value})");

            // 効果実行（コルーチン）
            yield return StartCoroutine(ApplyEffect(e.type, e.value));

            if (isAuto)
            {
                yield return new WaitForSeconds(0.6f);
            }
            else
            {
                // 未対応や手動操作でも、必ず続ける
                yield return new WaitForSeconds(0.1f);
            }
        }

        if (processWindow != null)
            processWindow.ShowMessage("効果処理完了！");

        yield return new WaitForSeconds(0.4f);

        if (discardManager != null)
        {
            discardManager.AddToDiscard(myData);
            Destroy(gameObject);
        }
    }

    private bool IsAutoEffect(string type)
    {
        switch (type)
        {
            case "Draw":
            case "ManaBoost":
            case "ManaRecover":
            case "LifeAdd":
            case "EndTurn":
            case "EndTurnIfMyTurn":
            case "Defence":
                return true;
            default:
                return false;
        }
    }

    IEnumerator ApplyEffect(string type, string value)
    {
        if (string.IsNullOrEmpty(type))
            yield break;

        switch (type)
        {
            case "Attack":
                yield return StartCoroutine(DoAttack());
                break;

            case "Draw":
                if (int.TryParse(value, out int drawCount))
                    yield return StartCoroutine(DoDrawRoutine(drawCount));
                break;

            case "ManaBoost":
                if (int.TryParse(value, out int boost))
                    yield return StartCoroutine(DoManaBoostRoutine(boost));
                break;

            case "ManaRecover":
                if (int.TryParse(value, out int recover))
                    yield return StartCoroutine(DoManaRecoverRoutine(recover));
                break;

            case "LifeAdd":
                if (int.TryParse(value, out int life))
                    yield return StartCoroutine(DoLifeAddRoutine(life));
                break;

            case "RecoverDiscard":
                if (int.TryParse(value, out int count))
                    yield return StartCoroutine(DoRecoverDiscardRoutine(count));
                break;

            case "EndTurn":
                yield return StartCoroutine(DoEndTurnRoutine());
                break;

            default:
                // 未対応でも必ず Next を出して止める
                yield return EffectProcessWindow.Instance.ShowProcess(
                    $"未対応の効果: {type} はまだ実装されていません。"
                );
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
            player.UpdateEnergyUI();
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
    private IEnumerator DoDrawRoutine(int n)
    {
        yield return EffectProcessWindow.Instance.ShowProcess($"カードを {n} 枚引きます。");
        DoDraw(n);         // 既存の void 関数
        yield break;
    }

    private IEnumerator DoManaBoostRoutine(int x)
    {
        yield return EffectProcessWindow.Instance.ShowProcess($"最大マナを {x} 増やします。");
        DoManaBoost(x);
        yield break;
    }

    private IEnumerator DoManaRecoverRoutine(int x)
    {
        yield return EffectProcessWindow.Instance.ShowProcess($"マナを {x} 回復します。");
        DoManaRecover(x);
        yield break;
    }

    private IEnumerator DoLifeAddRoutine(int x)
    {
        yield return EffectProcessWindow.Instance.ShowProcess($"ライフを {x} 増やします。");
        DoLifeAdd(x);
        yield break;
    }

    private IEnumerator DoRecoverDiscardRoutine(int x)
    {
        yield return EffectProcessWindow.Instance.ShowProcess($"捨て札から {x} 枚回収します。");
        DoRecoverDiscard(x);
        yield break;
    }

    private IEnumerator DoEndTurnRoutine()
    {
        yield return EffectProcessWindow.Instance.ShowProcess("ターンを終了します。");
        DoEndTurn();
        yield break;
    }

    IEnumerator DoAttack()
    {
        var gm = FindAnyObjectByType<GameManager>();
        if (gm == null) yield break;

        // 攻撃対象は仮で player2（または逆）
        PlayerManager attacker = player;
        PlayerManager defender = (gm.player1 == player) ? gm.player2 : gm.player1;

        yield return BattleManager.Instance.HandleAttack(attacker, defender, myData);
    }
}
