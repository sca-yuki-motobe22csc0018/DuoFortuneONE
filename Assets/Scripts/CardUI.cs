using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public enum CardUISource
{
    DiscardZone,
    RecoverZone
}

public class CardUI : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    [Header("UI Components")]
    public Image cardImage;
    public Image typeImage;
    public Text nameText;
    public Text costText;
    public Text countText;
    public Text textText;

    [Header("選択ハイライト枠")]
    [SerializeField] private Image highlightFrame; // 枠用Image（Raycast Target OFF、初期はdisabled）

    private CardGenerator.CardData cardData;
    private DiscardManager discardManager;

    private float pointerDownTime;
    private bool pointerHeld;
    private bool longPressTriggered;

    [Header("長押し判定時間(秒)")]
    public float longPressThreshold = 0.5f;

    // 自分が属するゾーン
    public CardUISource sourceZone = CardUISource.DiscardZone;

    /// <summary>
    /// カードUIの内容をセット
    /// </summary>
    public void SetCard(CardGenerator.CardData data, int count = 1, DiscardManager manager = null, CardUISource zone = CardUISource.DiscardZone)
    {
        cardData = data;
        discardManager = manager;
        sourceZone = zone;

        if (nameText) nameText.text = data.name;
        if (costText) costText.text = data.cost.ToString();
        if (textText) textText.text = data.text;

        if (cardImage)
        {
            var imageSprite = Resources.Load<Sprite>("CardImages/" + data.image);
            if (imageSprite) cardImage.sprite = imageSprite;
        }
        if (typeImage)
        {
            var typeSprite = Resources.Load<Sprite>("CardTypes/Card_Type_" + data.type);
            if (typeSprite) typeImage.sprite = typeSprite;
        }
        if (countText)
        {
            countText.gameObject.SetActive(count > 1);
            countText.text = "×" + count;
        }

        SetHighlight(false); // 初期は枠OFF
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        pointerHeld = true;
        longPressTriggered = false;
        pointerDownTime = Time.unscaledTime;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        // 長押し未発動 → 短押し扱い
        if (!longPressTriggered && discardManager != null && discardManager.IsRecoverMode)
        {
            if (sourceZone == CardUISource.DiscardZone)
            {
                // 捨て札ゾーン → 回収ゾーン
                discardManager.MoveCardToRecover(cardData);
            }
            else if (sourceZone == CardUISource.RecoverZone)
            {
                // 回収ゾーン → 捨て札ゾーン
                discardManager.MoveCardBackToDiscard(cardData);
            }
        }

        pointerHeld = false;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        pointerHeld = false;
    }

    private void Update()
    {
        if (pointerHeld && !longPressTriggered)
        {
            float held = Time.unscaledTime - pointerDownTime;
            if (held >= longPressThreshold)
            {
                longPressTriggered = true;
                // ★ 長押しで詳細表示（回収モードでも通常時でもOK）
                if (cardData != null && CardDetailPanel.Instance != null)
                {
                    CardDetailPanel.Instance.Show(cardData);
                }
            }
        }
    }

    private void OnDisable()
    {
        pointerHeld = false;
        longPressTriggered = false;
        pointerDownTime = 0f;
    }

    public void SetHighlight(bool active)
    {
        if (highlightFrame != null)
            highlightFrame.enabled = active;
    }
}
