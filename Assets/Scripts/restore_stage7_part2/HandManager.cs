using System.Collections.Generic;
using UnityEngine;

public class HandManager : MonoBehaviour
{
    [Header("Hand Cards")]
    public List<GameObject> handCards = new List<GameObject>();

    [Header("Layout Settings")]
    public float cardSpacing = 2.0f;
    public float handY = -4f;
    public float maxWidth = 12f;

    [Header("Arc Settings")]
    public float maxAngle = 15f;
    public float curveHeight = 1.5f;
    public bool arcUp = false;

    [Header("Scale Settings")]
    public float normalScale = 0.33f;
    public float hoverAllScale = 1.1f;
    public float hoverCardScale = 1.3f;

    [Header("Sorting Settings")]
    public int baseSortingOrder = 100;
    public int cardOrderStep = 10;
    public int hoverSortingOffset = 1000;

    [Header("Hover Settings")]
    public float hoverCopyY = 2f;
    public float hoverCopyScale = 2.0f;

    [Header("Prefabs")]
    public GameObject spriteCardPrefab;

    private GameObject hoverCardGO;
    private GameObject draggingCard;

    public DiscardManager discardManager;

    void Awake()
    {
        if (discardManager == null)
            discardManager = FindAnyObjectByType<DiscardManager>();
    }

    void Update()
    {
        // ドラッグ中のカードは手札レイアウトに含めない
        UpdateCardPositions(draggingCard);
        HandleHover();
    }

    public void AddCard(GameObject card)
    {
        if (card == null) return;

        card.transform.SetParent(transform);
        if (!handCards.Contains(card)) handCards.Add(card);

        handCards.Sort((a, b) =>
        {
            CardGenerator ca = a.GetComponent<CardGenerator>();
            CardGenerator cb = b.GetComponent<CardGenerator>();
            if (ca == null || cb == null) return 0;
            return ca.cardID.CompareTo(cb.cardID);
        });

        UpdateCardPositions();
    }

    public void RemoveCard(GameObject card)
    {
        if (card == null) return;
        if (!handCards.Contains(card)) return;

        handCards.Remove(card);
        UpdateCardPositions();

        // ▼ ここを CardData ベースで捨て札へ
        DiscardFromHand(card);
    }

    public void PlayCard(GameObject card)
    {
        if (card == null) return;
        if (!handCards.Contains(card)) return;

        handCards.Remove(card);
        UpdateCardPositions();

        // 通常のプレイ時は CardGenerator.TryPlayCard() が処理する想定だが
        // ここ経由で捨てたいケースにも対応しておく
        DiscardFromHand(card);
    }

    /// <summary>
    /// 手札の GameObject を CardData に変換して捨て札（データ）へ送る
    /// </summary>
    private void DiscardFromHand(GameObject card)
    {
        if (card == null) return;

        CardGenerator cg = card.GetComponent<CardGenerator>();
        if (cg != null && discardManager != null)
        {
            var data = cg.GetCardData(); // CardGenerator に実装済み
            if (data != null)
            {
                discardManager.AddToDiscard(data); // ★ CardData を渡す
            }
        }

        // 実物は破棄
        Destroy(card);
    }

    public void UpdateCardPositions(GameObject excludeCard = null)
    {
        int count = handCards.Count;
        if (count == 0) return;

        float totalWidth = cardSpacing * (count - 1);
        float scaleFactor = 1f;

        if (totalWidth > maxWidth)
        {
            scaleFactor = maxWidth / totalWidth;
            totalWidth = maxWidth;
        }

        int visibleIndex = 0;
        for (int i = 0; i < count; i++)
        {
            GameObject card = handCards[i];
            if (card == null || card == excludeCard) continue;

            float x, y, angle;

            if (count == 1)
            {
                x = 0;
                y = handY;
                angle = 0;
            }
            else
            {
                float t = (float)visibleIndex / (count - 1);
                x = -totalWidth / 2 + visibleIndex * cardSpacing * scaleFactor;

                if (arcUp)
                {
                    y = handY - Mathf.Pow(t - 0.5f, 2) * curveHeight + curveHeight;
                    angle = (t - 0.5f) * maxAngle * 2;
                }
                else
                {
                    y = handY + Mathf.Pow(t - 0.5f, 2) * curveHeight;
                    angle = -(t - 0.5f) * maxAngle * 2;
                }
            }

            card.transform.localPosition = new Vector3(x, y, 0);
            card.transform.localRotation = Quaternion.Euler(0, 0, angle);
            card.transform.localScale = Vector3.one * normalScale;

            CardGenerator cg = card.GetComponent<CardGenerator>();
            if (cg != null)
            {
                cg.baseSortingOrder = baseSortingOrder + visibleIndex * cardOrderStep;
                cg.SetChildSortingOrders();
            }

            SpriteRenderer sr = card.GetComponent<SpriteRenderer>();
            if (sr != null) sr.sortingOrder = baseSortingOrder + visibleIndex * cardOrderStep;

            Canvas canvas = card.GetComponent<Canvas>();
            if (canvas != null)
            {
                canvas.overrideSorting = true;
                canvas.sortingOrder = baseSortingOrder + visibleIndex * cardOrderStep;
            }

            visibleIndex++;
        }
    }

    void HandleHover()
    {
        if (Camera.main == null) return;

        Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
        mousePos.z = 0;

        GameObject hovered = null;

        foreach (GameObject card in handCards)
        {
            if (card == null || card == draggingCard) continue;

            Collider2D col = card.GetComponent<Collider2D>();
            if (col != null && col.OverlapPoint(mousePos))
            {
                hovered = card;
                break;
            }
        }

        if (hoverCardGO != null)
        {
            Destroy(hoverCardGO);
            hoverCardGO = null;
        }

        for (int i = 0; i < handCards.Count; i++)
        {
            GameObject card = handCards[i];
            if (card == null) continue;

            CardGenerator cg = card.GetComponent<CardGenerator>();
            if (cg == null) continue;

            if (hovered == null)
            {
                card.transform.localScale = Vector3.one * normalScale;
                cg.baseSortingOrder = baseSortingOrder + i * cardOrderStep;
                cg.SetChildSortingOrders();
            }
            else if (card == hovered)
            {
                card.transform.localScale = Vector3.one * normalScale * hoverCardScale;
                cg.baseSortingOrder = baseSortingOrder + i * cardOrderStep + hoverSortingOffset;
                cg.SetChildSortingOrders();

                // 拡大プレビュー（必要なければ削除OK）
                hoverCardGO = Instantiate(card, transform.parent);
                if (Camera.main != null)
                {
                    Vector3 centerPos = Camera.main.ScreenToWorldPoint(new Vector3(Screen.width / 2, Screen.height / 2, 10f));
                    centerPos.y = hoverCopyY;
                    hoverCardGO.transform.position = centerPos;
                }
                hoverCardGO.transform.localScale = Vector3.one * hoverCopyScale;
                hoverCardGO.transform.localRotation = Quaternion.identity;

                CardGenerator copyCG = hoverCardGO.GetComponent<CardGenerator>();
                if (copyCG != null)
                {
                    copyCG.baseSortingOrder += hoverSortingOffset * 2;
                    copyCG.SetChildSortingOrders();
                }
            }
            else
            {
                card.transform.localScale = Vector3.one * normalScale * hoverAllScale;
                cg.baseSortingOrder = baseSortingOrder + i * cardOrderStep;
                cg.SetChildSortingOrders();
            }
        }
    }

    // ドラッグ用フック（必要に応じて使用）
    public void SetDraggingCard(GameObject card) => draggingCard = card;
    public void ClearDraggingCard() => draggingCard = null;

    /// <summary>
    /// CardData から新しいカード（SpriteCard）を生成して手札に加える
    /// </summary>
    public void AddCardFromData(CardGenerator.CardData data)
    {
        if (data == null)
        {
            Debug.LogWarning("AddCardFromData に null が渡されました。");
            return;
        }

        // --- SpriteCardプレハブを生成 ---
        GameObject newCard = Instantiate(spriteCardPrefab, transform);
        newCard.transform.localScale = Vector3.one * normalScale;

        // --- CardGeneratorでカードデータを適用 ---
        var cg = newCard.GetComponent<CardGenerator>();
        if (cg != null)
        {
            cg.ApplyCardData(data);
        }
        else
        {
            Debug.LogError("SpriteCard に CardGenerator が見つかりません！");
        }

        // --- リストに追加して並び替え更新 ---
        handCards.Add(newCard);
        UpdateCardPositions();

        Debug.Log($"手札にカード {data.name} を追加しました。");
    }
}
