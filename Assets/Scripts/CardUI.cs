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

    [Header("�I���n�C���C�g�g")]
    [SerializeField] private Image highlightFrame; // �g�pImage�iRaycast Target OFF�A������disabled�j

    private CardGenerator.CardData cardData;
    private DiscardManager discardManager;

    private float pointerDownTime;
    private bool pointerHeld;
    private bool longPressTriggered;

    [Header("���������莞��(�b)")]
    public float longPressThreshold = 0.5f;

    // ������������]�[��
    public CardUISource sourceZone = CardUISource.DiscardZone;

    /// <summary>
    /// �J�[�hUI�̓��e���Z�b�g
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
            countText.text = "�~" + count;
        }

        SetHighlight(false); // �����͘gOFF
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        pointerHeld = true;
        longPressTriggered = false;
        pointerDownTime = Time.unscaledTime;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        // ������������ �� �Z��������
        if (!longPressTriggered && discardManager != null && discardManager.IsRecoverMode)
        {
            if (sourceZone == CardUISource.DiscardZone)
            {
                // �̂ĎD�]�[�� �� ����]�[��
                discardManager.MoveCardToRecover(cardData);
            }
            else if (sourceZone == CardUISource.RecoverZone)
            {
                // ����]�[�� �� �̂ĎD�]�[��
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
                // �� �������ŏڍו\���i������[�h�ł��ʏ펞�ł�OK�j
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
