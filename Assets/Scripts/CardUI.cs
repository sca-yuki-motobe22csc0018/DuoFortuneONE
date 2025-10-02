using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

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
    public TMP_Text nameText;
    public TMP_Text costText;
    public TMP_Text countText;
    public TMP_Text textText;

    [Header("選択ハイライト枠")]
    [SerializeField] private Image highlightFrame; 

    private CardGenerator.CardData cardData;
    private DiscardManager discardManager;

    private float pointerDownTime;
    private bool pointerHeld;
    private bool longPressTriggered;

    [Header("長押し判定時間(秒)")]
    public float longPressThreshold = 0.5f;

    public CardUISource sourceZone = CardUISource.DiscardZone;

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

        SetHighlight(false);
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        pointerHeld = true;
        longPressTriggered = false;
        pointerDownTime = Time.unscaledTime;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!longPressTriggered && discardManager != null && discardManager.IsRecoverMode)
        {
            if (sourceZone == CardUISource.DiscardZone)
            {
                discardManager.MoveCardToRecover(cardData);
            }
            else if (sourceZone == CardUISource.RecoverZone)
            {
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
