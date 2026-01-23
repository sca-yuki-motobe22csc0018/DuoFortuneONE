using System.Collections.Generic;
using System.Text;
using System.IO;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections; // ★ 追加（コルーチン用）
using Fusion;

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
    public bool skipAutoDiscard = false;
    public bool isDefenceWindowUse = false; // ★ DEFENCEウインドウ経由で使われたか

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

        if (player == null) player = GetComponentInParent<PlayerManager>();
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

        // ★ cardID を必ず同期
        cardID = data.id;

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
        string path = Application.streamingAssetsPath + "/Card_Data_beta.csv";
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
                else if (c == ',')
                {
                    currentRow.Add(currentField.ToString());
                    currentField.Clear();
                }
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

                // ★ 追加：使用が成立した瞬間に手札枚数を Host 確定＆両画面更新
                if (used && player != null)
                {
                    player.NotifyHandChangedForBothSides();
                }

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
        if (myData == null) return false;

        // ▼ 追加：手札カードは「自分のターン」だけ使用可能
        // （Block/ライフから来たカード/DefenceWindow等は別経路なのでここでは止めない）
        var gm = GameManager.Instance ?? FindAnyObjectByType<GameManager>();
        if (gm != null)
        {
            if (!gm.IsLocalPlayersTurn())
            {
                Debug.Log("自分のターンではないため、手札カードは使用できません。");
                return false;
            }

            // ▼ 追加：効果処理中は手札カード使用禁止
            if (gm.IsHandCardUseLocked())
            {
                Debug.Log("効果処理中のため、手札カードは使用できません。");
                return false;
            }
        }

        // ▼ 追加：Blockカードは通常使用禁止（攻撃への反応時のみ）
        if (myData.type == "B" || myData.type == "BLOCK")
        {
            Debug.Log("Blockカードは手札から通常使用できません（攻撃への反応時のみ）。");
            return false;
        }
        // ▼ 追加：EXカードは通常使用禁止
        if (myData.type == "E" || myData.type == "EX")
        {
            Debug.Log("EXカードは手札から通常使用できません。");
            return false;
        }

        // ★ ローカルで「マナが足りているか」だけチェック（Networked のスナップショット）
        if (player.currentMana < myData.cost)
        {
            Debug.Log("マナが足りません！（ローカルチェック）");
            return false;
        }

        // ★ Host に正式な支払いを依頼（Host自身も含む）
        var runner = FindAnyObjectByType<NetworkRunner>();

        if (gm != null && runner != null)
        {
            gm.RPC_RequestSpendMana(runner.LocalPlayer, myData.cost);
        }
        else
        {
            // ネットワーク周りが見つからない場合の保険としてローカルで支払い
            player.SpendMana(myData.cost);
        }

        // ★ 使用確定時、GameObjectは非アクティブ化せず"見た目だけ"即座に消す
        HideVisualsForUsing();

        // ▼ 追加：効果処理中ロック（手札カードのみ）
        if (gm != null) gm.SetHandCardUseLocked(true);

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
        var gmLock = GameManager.Instance ?? FindAnyObjectByType<GameManager>();

        // 手札カード使用ロック解除は必ず最後に行う（途中return/Destroy対策）
        try
        {
            var effects = new List<(string type, string value)>()
        {
            (myData.effectType1, myData.effectValue1),
            (myData.effectType2, myData.effectValue2),
            (myData.effectType3, myData.effectValue3),
            (myData.effectType4, myData.effectValue4),
            (myData.effectType5, myData.effectValue5),
            (myData.effectType6, myData.effectValue6),
        };

            bool hasAttackEffect = false;
            for (int i = 0; i < effects.Count; i++)
            {
                if (effects[i].type == "Attack")
                {
                    hasAttackEffect = true;
                    break;
                }
            }

            foreach (var e in effects)
            {
                if (string.IsNullOrEmpty(e.type)) continue;

                bool isAuto = IsAutoEffect(e.type);

                if (processWindow != null && !hasAttackEffect)
                    processWindow.ShowMessage($"効果実行中: {e.type} ({e.value})");

                yield return StartCoroutine(ApplyEffect(e.type, e.value));

                if (isAuto)
                    yield return new WaitForSeconds(0.6f);
                else
                    yield return new WaitForSeconds(0.1f);
            }

            if (processWindow != null && !hasAttackEffect)
                processWindow.ShowMessage("効果処理完了！");

            yield return new WaitForSeconds(0.4f);

            if (!skipAutoDiscard && myData != null && myData.type != "B" && myData.type != "BLOCK")
            {
                var gm = GameManager.Instance ?? FindAnyObjectByType<GameManager>();
                var r = FindAnyObjectByType<NetworkRunner>();

                if (gm != null && r != null)
                {
                    gm.RPC_RequestAddDiscard(r.LocalPlayer, myData.id);
                }
                else if (discardManager != null)
                {
                    discardManager.AddToDiscard(myData);
                }
            }

            if (player != null) player.NotifyHandChangedForBothSides();
            Destroy(gameObject);
        }
        finally
        {
            if (gmLock != null) gmLock.SetHandCardUseLocked(false);
        }
    }


    private bool IsAutoEffect(string type)
    {
        switch (type)
        {
            case "Attack":
            case "Draw":
            case "ManaBoost":
            case "ManaReduceSelf":
            case "ManaReduceOpponent":
            case "ManaReduceIfMyTurn":
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

            case "ManaReduceSelf":
                if (int.TryParse(value, out int reduceSelf))
                    yield return StartCoroutine(DoManaReduceSelfRoutine(reduceSelf));
                break;

            case "ManaReduceOpponent":
                if (int.TryParse(value, out int reduceOpp))
                    yield return StartCoroutine(DoManaReduceOpponentRoutine(reduceOpp));
                break;

            case "ManaReduceIfMyTurn":
                if (int.TryParse(value, out int reduceIfMyTurn))
                    yield return StartCoroutine(DoManaReduceIfMyTurnRoutine(reduceIfMyTurn));
                break;

            case "ManaRecover":
                // ★ "ALL" の場合は特別扱い（全回復）
                if (value == "ALL")
                {
                    yield return StartCoroutine(DoManaRecoverRoutine(-1));
                }
                else if (int.TryParse(value, out int recover))
                {
                    yield return StartCoroutine(DoManaRecoverRoutine(recover));
                }
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

            case "RandomDiscardSelf":
                {
                    int cnt = 1;
                    int.TryParse(value, out cnt);
                    cnt = Mathf.Max(1, cnt);

                    var gm = GameManager.Instance ?? FindAnyObjectByType<GameManager>();
                    var runner = FindAnyObjectByType<NetworkRunner>();
                    if (gm != null && runner != null)
                    {
                        gm.RPC_RequestRandomDiscard(runner.LocalPlayer, runner.LocalPlayer, cnt);
                    }
                    break;
                }

            case "RandomDiscardOpponent":
                {
                    int cnt = 1;
                    int.TryParse(value, out cnt);
                    cnt = Mathf.Max(1, cnt);

                    var gm = GameManager.Instance ?? FindAnyObjectByType<GameManager>();
                    var runner = FindAnyObjectByType<NetworkRunner>();
                    if (gm != null && runner != null && player != null && player.opponent != null && player.opponent.Object != null)
                    {
                        gm.RPC_RequestRandomDiscard(runner.LocalPlayer, player.opponent.Object.InputAuthority, cnt);
                    }
                    break;
                }
            case "SelectDiscardSelf":
                {
                    // "ALL" 対応：全部捨てる
                    if (value == "ALL")
                    {
                        yield return StartCoroutine(DoSelectDiscardSelfRoutine(-1));
                    }
                    else if (int.TryParse(value, out int cnt))
                    {
                        cnt = Mathf.Max(1, cnt);
                        yield return StartCoroutine(DoSelectDiscardSelfRoutine(cnt));
                    }
                    break;
                }




            case "Defence":
                yield return StartCoroutine(DoDefenceRoutine());
                break;

            default:
                // 未対応でも必ず Next を出して止める
                yield return EffectProcessWindow.Instance.ShowProcessAuto(
                    $"未対応の効果: {type} はまだ実装されていません。", 0.6f, false
                );
                break;
        }
    }

    // ★★ ここをネット対応版に変更済み ★★
    void DoDraw(int count)
    {
        if (count <= 0) return;

        var gm = FindAnyObjectByType<GameManager>();
        var runner = FindAnyObjectByType<NetworkRunner>();

        if (gm == null || runner == null)
        {
            Debug.LogWarning("DoDraw: GameManager または NetworkRunner が見つかりません。");
            return;
        }

        // ★ Host/Client 関係なく、「このプレイヤーが効果ドローしたい」と Host に依頼
        gm.RPC_RequestEffectDraw(runner.LocalPlayer, count);
    }

    void DoManaBoost(int amount)
    {
        if (player == null || amount <= 0)
            return;

        var gm = GameManager.Instance ?? FindAnyObjectByType<GameManager>();
        var runner = FindAnyObjectByType<NetworkRunner>();

        if (gm != null && runner != null)
        {
            // ★ Host に「最大マナを増やしたい」と依頼
            gm.RPC_RequestEffectManaBoost(runner.LocalPlayer, amount);
        }
        else
        {
            // オフラインや何かがおかしい時の保険としてローカル処理
            player.IncreaseMaxManaOnly(amount);
            player.UpdateEnergyUI();
            player.UpdateOpponentUI();
        }
    }

    void DoManaRecover(int amount)
    {
        if (player == null)
            return;

        bool isAll = false;
        int recoverAmount = amount;

        // ★ amount < 0 は「ALL（全回復）」扱い
        if (amount < 0)
        {
            isAll = true;
            recoverAmount = 0;
        }

        var gm = GameManager.Instance ?? FindAnyObjectByType<GameManager>();
        var runner = FindAnyObjectByType<NetworkRunner>();

        if (gm != null && runner != null)
        {
            // ★ Host に「マナ回復したい」と依頼（ALL の場合は isAll=true）
            gm.RPC_RequestEffectManaRecover(runner.LocalPlayer, recoverAmount, isAll);
        }
        else
        {
            // オフライン／保険としてローカル処理
            if (isAll)
            {
                player.currentMana = player.maxMana;
            }
            else if (recoverAmount > 0)
            {
                player.currentMana = Mathf.Min(player.currentMana + recoverAmount, player.maxMana);
            }
            player.UpdateEnergyUI();
            player.UpdateOpponentUI();
        }
    }
    void DoManaReduceSelf(int amount)
    {
        if (player == null || amount <= 0)
            return;

        var gm = GameManager.Instance ?? FindAnyObjectByType<GameManager>();
        var runner = FindAnyObjectByType<NetworkRunner>();

        if (gm != null && runner != null)
        {
            gm.RPC_RequestEffectManaReduceSelf(runner.LocalPlayer, amount);
        }
        else
        {
            player.DecreaseMaxManaOnly(amount);
            player.UpdateEnergyUI();
            player.UpdateOpponentUI();
        }
    }

    void DoManaReduceOpponent(int amount)
    {
        if (player == null || amount <= 0)
            return;

        var gm = GameManager.Instance ?? FindAnyObjectByType<GameManager>();
        var runner = FindAnyObjectByType<NetworkRunner>();

        if (gm != null && runner != null)
        {
            gm.RPC_RequestEffectManaReduceOpponent(runner.LocalPlayer, amount);
        }
        else
        {
            if (player.opponent != null)
            {
                player.opponent.DecreaseMaxManaOnly(amount);
                player.UpdateEnergyUI();
                player.UpdateOpponentUI();
            }
        }
    }

    void DoManaReduceIfMyTurn(int amount)
    {
        if (player == null || amount <= 0)
            return;

        var gm = GameManager.Instance ?? FindAnyObjectByType<GameManager>();
        var runner = FindAnyObjectByType<NetworkRunner>();

        if (gm != null && runner != null)
        {
            gm.RPC_RequestEffectManaReduceIfMyTurn(runner.LocalPlayer, amount);
        }
        else
        {
            // オフラインなど：自分のターン判定ができないのでそのまま適用
            player.DecreaseMaxManaOnly(amount);
            player.UpdateEnergyUI();
            player.UpdateOpponentUI();
        }
    }
    void DoLifeAdd(int amount)
    {
        if (amount <= 0 || player == null)
            return;

        var gm = GameManager.Instance ?? FindAnyObjectByType<GameManager>();
        var runner = FindAnyObjectByType<NetworkRunner>();

        if (gm != null && runner != null)
        {
            // Host に「このプレイヤーが LifeAdd をした」と依頼
            gm.RPC_RequestEffectLifeAdd(runner.LocalPlayer, amount);
        }
        else if (player.lifeManager != null)
        {
            // オフライン／何かがおかしい時の保険としてローカル処理
            for (int i = 0; i < amount; i++)
                player.lifeManager.AddLife();
        }
    }

    void DoEndTurn()
    {
        var gm = FindAnyObjectByType<GameManager>();
        if (gm != null) gm.OnEndTurn();
    }

    private IEnumerator DoDrawRoutine(int n)
    {
        yield return EffectProcessWindow.Instance.ShowProcessAuto($"カードを {n} 枚引きます。", 0.6f, false);
        DoDraw(n);         // ★ 中身がネット対応になった
        yield break;
    }

    private IEnumerator DoManaBoostRoutine(int x)
    {
        yield return EffectProcessWindow.Instance.ShowProcessAuto($"最大マナを {x} 増やします。", 0.6f, false);
        DoManaBoost(x);
        yield break;
    }
    IEnumerator DoManaReduceSelfRoutine(int x)
    {
        yield return EffectProcessWindow.Instance.ShowProcessAuto($"最大マナを {x} 減らします。", 0.6f, false);
        DoManaReduceSelf(x);
        yield break;
    }

    IEnumerator DoManaReduceOpponentRoutine(int x)
    {
        yield return EffectProcessWindow.Instance.ShowProcessAuto($"相手の最大マナを {x} 減らします。", 0.6f, false);
        DoManaReduceOpponent(x);
        yield break;
    }

    IEnumerator DoManaReduceIfMyTurnRoutine(int x)
    {
        yield return EffectProcessWindow.Instance.ShowProcessAuto($"自分のターンなら最大マナを {x} 減らします。", 0.6f, false);
        DoManaReduceIfMyTurn(x);
        yield break;
    }

    private IEnumerator DoManaRecoverRoutine(int x)
    {
        if (x < 0)
        {
            // ALL の場合
            yield return EffectProcessWindow.Instance.ShowProcessAuto("マナを全回復します。", 0.6f, false);
        }
        else
        {
            yield return EffectProcessWindow.Instance.ShowProcessAuto($"マナを {x} 回復します。", 0.6f, false);
        }

        DoManaRecover(x);
        yield break;
    }

    private IEnumerator DoLifeAddRoutine(int x)
    {
        yield return EffectProcessWindow.Instance.ShowProcessAuto($"ライフを {x} 増やします。", 0.6f, false);
        DoLifeAdd(x);
        yield break;
    }


    /// <summary>
    /// DEFENCE効果：DEFENCEウインドウ経由の時だけ
    /// ①ライフが0ならライフを1追加 → ②マナを2回復
    /// </summary>
    private IEnumerator DoDefenceRoutine()
    {
        // 手札から普通に使った場合は何もしない（次の効果へ）
        if (!isDefenceWindowUse)
            yield break;

        var gm = GameManager.Instance ?? FindAnyObjectByType<GameManager>();
        var runner = FindAnyObjectByType<NetworkRunner>();

        // ① ライフが0ならライフを1追加
        if (EffectProcessWindow.Instance != null)
            yield return EffectProcessWindow.Instance.ShowProcessAuto("① ライフが0ならライフを1追加します。", 0.6f, false);

        if (gm != null && runner != null)
        {
            gm.RPC_RequestEffectDefenceLifeIfZero(runner.LocalPlayer);
        }
        else
        {
            // オフライン／保険としてローカル処理
            if (player != null && player.lifeManager != null)
            {
                int lifeCount = GetLifeCountSafe(player);
                if (lifeCount <= 0)
                {
                    player.lifeManager.AddLife();
                }
            }
        }

        // 少し待ってUI反映を安定させる
        yield return new WaitForSeconds(0.2f);

        // ② マナを2回復
        if (EffectProcessWindow.Instance != null)
            yield return EffectProcessWindow.Instance.ShowProcessAuto("② マナを2回復します。", 0.6f, false);

        if (gm != null && runner != null)
        {
            gm.RPC_RequestEffectManaRecover(runner.LocalPlayer, 2, false);
        }
        else if (player != null)
        {
            player.currentMana = Mathf.Min(player.maxMana, player.currentMana + 2);
            player.UpdateEnergyUI();
            player.UpdateOpponentUI();
        }

        yield break;
    }

    // ★ LifeManager の実装差異に強い「現在ライフ枚数」取得（オフライン保険用）
    private int GetLifeCountSafe(PlayerManager pm)
    {
        if (pm == null || pm.lifeManager == null) return 0;

        object lm = pm.lifeManager;

        // よくあるプロパティ名
        var t = lm.GetType();
        var p1 = t.GetProperty("LifeCount");
        if (p1 != null)
        {
            try { return (int)p1.GetValue(lm); } catch { }
        }

        var p2 = t.GetProperty("lifeCount");
        if (p2 != null)
        {
            try { return (int)p2.GetValue(lm); } catch { }
        }

        // よくあるフィールド名（Listなど）
        foreach (var fname in new[] { "lifeCards", "lifeList", "cards", "life" })
        {
            var f = t.GetField(fname);
            if (f != null)
            {
                try
                {
                    var v = f.GetValue(lm);
                    if (v is System.Collections.ICollection col) return col.Count;
                }
                catch { }
            }
        }

        // 最後の手段：Count/Lengthを探す
        foreach (var pname in new[] { "Count", "Length" })
        {
            var p = t.GetProperty(pname);
            if (p != null)
            {
                try
                {
                    var v = p.GetValue(lm);
                    if (v is int i) return i;
                }
                catch { }
            }
        }

        return 0;
    }


    private IEnumerator DoRecoverDiscardRoutine(int x)
    {
        var discard = FindAnyObjectByType<DiscardManager>();
        if (discard == null)
        {
            yield break;
        }

        // メッセージを表示
        yield return EffectProcessWindow.Instance.ShowProcessAuto($"捨て札から {x} 枚回収します。", 0.6f, false);

        // 回収モード開始
        discard.StartRecoverMode(player, x);

        // ★ OKボタンが押されるまで待機
        yield return new WaitUntil(() => discard.IsRecoverComplete);

        // （OKが押されたら次の処理に進む）
        yield break;
    }

    private IEnumerator DoEndTurnRoutine()
    {
        yield return EffectProcessWindow.Instance.ShowProcessAuto("ターンを終了します。", 0.6f, false);
        DoEndTurn();
        yield break;
    }

    private IEnumerator DoAttack()
    {
        var gm = GameManager.Instance ?? FindAnyObjectByType<GameManager>();
        var r = FindAnyObjectByType<NetworkRunner>();
        if (gm == null || r == null) yield break;

        int requestId = NextAttackRequestId();
        s_resolvedAttack.Remove(requestId);

        // ★ HostでもClientでも同じ：StateAuthorityに攻撃を依頼
        gm.RPC_RequestAttack(r.LocalPlayer, myData.id, requestId);

        while (!s_resolvedAttack.Contains(requestId))
            yield return null;

        s_resolvedAttack.Remove(requestId);
    }
    private static int s_attackRequestId = 0;
    private static readonly HashSet<int> s_resolvedAttack = new HashSet<int>();

    public static void NotifyAttackResolved(int requestId)
    {
        s_resolvedAttack.Add(requestId);
    }

    private static int NextAttackRequestId()
    {
        s_attackRequestId++;
        return s_attackRequestId;
    }
    private IEnumerator DoSelectDiscardSelfRoutine(int count)
    {
        var ui = FindAnyObjectByType<HandDiscardSelectManager>();
        if (ui == null) yield break;

        if (count < 0)
            yield return EffectProcessWindow.Instance.ShowProcessAuto("手札を全て捨てます。", 0.6f, false);
        else
            yield return EffectProcessWindow.Instance.ShowProcessAuto($"手札から {count} 枚捨てます。", 0.6f, false);

        ui.StartSelectDiscardMode(player, count);

        yield return new WaitUntil(() => ui.IsComplete);
    }

}